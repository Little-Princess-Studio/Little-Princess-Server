using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Ipc;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;
using Microsoft.VisualBasic;

namespace LPS.Core.Rpc
{
    /*
    TcpServer is common server for LPS inner usage.
    */
    public class TcpServer
    {
        public string Ip { get; private set; }
        public int Port { get; private set; }

        private bool stopFlag_;
        private readonly Dictionary<Connection, Task> connections_ = new();
        private readonly SandBox sandboxIo_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispatcher_;
        public Socket? Socket { get; private set; }
        public Action? OnInit { get; init; }
        public Action? OnDispose { get; init; }
        private readonly Dictionary<Socket, Connection> socketToConn_ = new();

        public Connection[] AllConnections => socketToConn_.Values.ToArray();
        private readonly ConcurrentQueue<(Connection, IMessage)> sendQueue_ = new();
        private uint serverEntityPackageId_;
        private readonly TimeCircle timeCircle_ = new(50, 1000);

        private readonly ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)> timeCircleQueue_ = new();

        public bool Stopped => stopFlag_;

        public TcpServer(string ip, int port)
        {
            this.Ip = ip;
            this.Port = port;

            msgDispatcher_ = new Dispatcher();
            bus_ = new Bus(msgDispatcher_);

            sandboxIo_ = SandBox.Create(this.IoHandler);
        }

        public void Run()
        {
            stopFlag_ = false;
            this.sandboxIo_.Run();
        }

        public void WaitForExit()
        {
            this.sandboxIo_.WaitForExit();
        }

        public void Stop()
        {
            Logger.Debug("stopped");
            this.stopFlag_ = true;
            try
            {
                this.Socket?.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                this.Socket?.Close();
            }
        }

        private void IoHandler()
        {
            #region listen port

            Logger.Debug($"[SOCKET] Start tcp server {this.Ip} {this.Port}");

            this.OnInit?.Invoke();

            var ipa = IPAddress.Parse(this.Ip);
            var ipe = new IPEndPoint(ipa, this.Port);

            this.Socket = new Socket(ipa.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.Socket.Bind(ipe);
            this.Socket.Listen(int.MaxValue);

            Logger.Debug("[SOCKET] Listen succ");

            #endregion

            #region init bus pump task

            var busPumpSandBox = SandBox.Create(this.PumpMessageHandler);
            busPumpSandBox.Run();

            #endregion

            #region init send queue task

            var sendQueueSandBox = SandBox.Create(this.SendQueueMessageHandler);
            sendQueueSandBox.Run();

            #endregion

            #region init timecircle enqueue task

            var timeCircleEnqueueSandBox = SandBox.Create(this.TimeCircleSyncMessageEnqueueHandler);
            timeCircleEnqueueSandBox.Run();

            #endregion

            #region init timecircle task

            var timeCircleSandBox = SandBox.Create(this.TimeCircleHandler);
            timeCircleSandBox.Run();

            #endregion

            #region message loop

            while (!stopFlag_)
            {
                Socket clientSocket;
                try
                {
                    clientSocket = this.Socket!.Accept();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Socket closed.");
                    break;
                }

                var ipEndPoint = (clientSocket.RemoteEndPoint as IPEndPoint)!;
                Logger.Debug($"New socket got {ipEndPoint.Address}:{ipEndPoint.Port}");

                var cancelTokenSource = new CancellationTokenSource();
                var conn = Connection.Create(clientSocket, cancelTokenSource);

                socketToConn_[clientSocket] = conn;

                conn.Connect();

                var task = this.HandleMessage(conn);
                connections_[conn] = task;

                task.ContinueWith(
                    _ => { Logger.Debug("Client Io Handler Exist"); }, 
                    cancelTokenSource.Token);
            }

            #endregion

            // cancel each task end
            Logger.Info($"[SOCKET] Close {connections_.Count} connections");
            foreach (var conn in this.connections_)
            {
                conn.Key.TokenSource.Cancel();
                conn.Value.Wait();
            }

            // wait pum task to exit
            Logger.Info("[EXIT] Close pump task");
            // busPumpTask.Wait();

            this.OnDispose?.Invoke();
        }

        public MailBox GetMailBox(Socket socket) => socketToConn_[socket].MailBox;

        private void PumpMessageHandler()
        {
            while (!stopFlag_)
            {
                try
                {
                    this.bus_.Pump();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Pump message failed.");
                }
                finally
                {
                    Thread.Sleep(50);
                }
            }
        }

        private void TimeCircleSyncMessageEnqueueHandler()
        {
            while (!stopFlag_)
            {
                while (!sendQueue_.IsEmpty)
                {
                    var res = timeCircleQueue_.TryDequeue(out var tp);
                    if (res)
                    {
                        var (keepOrder, delayTime, msg) = tp;
                        timeCircle_.AddPropertySyncMessage(msg, delayTime, keepOrder);
                    }
                }
                Thread.Sleep(1);
            }
        }

        private void TimeCircleHandler()
        {
            var lastTimeCircleTickTimestamp = DateTime.UtcNow;
            var currentTimeCircleTickTimestamp = DateTime.UtcNow;
            while (!stopFlag_)
            {
                var deltaTime = (currentTimeCircleTickTimestamp - lastTimeCircleTickTimestamp).Milliseconds;
                if (deltaTime > 50)
                {
                    lastTimeCircleTickTimestamp = currentTimeCircleTickTimestamp;
                    timeCircle_.Tick((uint) deltaTime);
                }

                Thread.Sleep(25);
            }
        }

        private void SendQueueMessageHandler()
        {
            while (!stopFlag_)
            {
                if (!sendQueue_.IsEmpty)
                {
                    var res = sendQueue_.TryDequeue(out var tp);
                    if (res)
                    {
                        var (conn, msg) = tp;
                        var id = serverEntityPackageId_++;
                        var pkg = PackageHelper.FromProtoBuf(msg, id);
                        var socket = conn.Socket;
                        try
                        {
                            socket.Send(pkg.ToBytes());
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Send msg failed.");
                            conn.TokenSource.Cancel();
                        }
                    }
                }

                Thread.Sleep(1);
            }
        }

        public void Send(IMessage msg, Connection conn)
        {
            try
            {
                sendQueue_.Enqueue((conn, msg));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Send Error.");
            }
        }

        public void AddMessageToTimeCircle(RpcPropertySyncMessage msg, uint delayTimeByMilliseconds, bool keepOrder)
            // => timeCircle_.AddPropertySyncMessage(msg, delayTimeByMilliseconds, keepOrder);
            => timeCircleQueue_.Enqueue((keepOrder, delayTimeByMilliseconds, msg));
        
        private async Task HandleMessage(Connection conn)
        {
            await RpcHelper.HandleMessage(
                conn,
                () => stopFlag_,
                (msg) => bus_.AppendMessage(msg),
                () => connections_.Remove(conn));
        }

        public void RegisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispatcher_.Register(key, callback);
        }

        public void UnregisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispatcher_.Unregister(key, callback);
        }
    }
}