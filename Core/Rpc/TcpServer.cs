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
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc
{
    /*
    TcpServer is common server for LPS inner usage.
    */
    public class TcpServer
    {
        public string IP { get; private set; }
        public int Port { get; private set; }
        
        private bool stopFlag_;
        private readonly Dictionary<Connection, Task> connections_ = new();
        private readonly SandBox sandboxIO_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispatcher_;
#nullable enable
        public Socket? Socket { get; private set; }
        public Action? OnInit { get; set; }
        public Action? OnDispose { get; set; }
#nullable disable
        private readonly Dictionary<Socket, Connection> socketToConn_ = new();

        public Connection[] AllConnections => socketToConn_.Values.ToArray();
        private readonly ConcurrentQueue<(Connection, IMessage)> sendQueue_ = new();
        private uint serverEntityPackageId_;
        
        public TcpServer(string ip, int port)
        {
            this.IP = ip;
            this.Port = port;

            msgDispatcher_ = new Dispatcher();
            bus_ = new Bus(msgDispatcher_);

            sandboxIO_ = SandBox.Create(this.IOHandler);
        }

        public void Run()
        {
            stopFlag_ = false;
            this.sandboxIO_.Run();
        }

        public void WaitForExit()
        {
            this.sandboxIO_.WaitForExit();
        }

        public void Stop()
        {
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

        private void IOHandler()
        {
            #region listen port

            Logger.Debug($"Start server {this.IP} {this.Port}");

            var ipa = IPAddress.Parse(this.IP);
            var ipe = new IPEndPoint(ipa, this.Port);

            this.Socket = new Socket(ipa.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.Socket.Bind(ipe);
            this.Socket.Listen(int.MaxValue);

            Logger.Debug("Listen succ");

            #endregion

            this.OnInit?.Invoke();

            #region init bus pump task
            var busPumpTask = new Task(this.PumpMessageHandler);
            busPumpTask.Start();
            #endregion

            #region init send queue task
            var sendQueueTask = new Task(this.SendQueueMessageHandler);
            sendQueueTask.Start();
            #endregion

            #region message loop
            while (!stopFlag_)
            {
                Socket clientSocket;
                try {
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

                var task = new Task(() =>
                {
                    this.HandleMessage(conn);
                }, cancelTokenSource.Token);

                connections_[conn] = task;
                task.Start();
            }
            #endregion

            // cancel each task end
            Logger.Info($"Close {connections_.Count} connections");
            foreach (var conn in this.connections_)
            {
                conn.Key.TokenSource.Cancel();
                conn.Value.Wait();
            }

            // wait pum task to exit
            Logger.Info("Close pump task");
            busPumpTask.Wait();

            this.OnDispose?.Invoke();
        }

        public MailBox GetMailBox(Socket socket) => socketToConn_[socket].MailBox;

        private void PumpMessageHandler()
        {
            try
            {
                while (!stopFlag_)
                {
                    this.bus_.Pump();
                    Thread.Sleep(0);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Pump message failed.");
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
                Thread.Sleep(0);
            }
        }

        public void Send(IMessage msg, Connection conn)
        {
            try {
                sendQueue_.Enqueue((conn, msg));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Send Error.");
            }
        }

        private async void HandleMessage(Connection conn)
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
