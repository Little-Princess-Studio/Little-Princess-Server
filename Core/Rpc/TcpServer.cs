using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Ipc;

namespace LPS.Core.Rpc
{
    /*
    TcpServer is common server for LPS inner usage.
    */
    public class TcpServer
    {
        public string IP { get; private set; }
        public int Port { get; private set; }
        private bool stopFlag_ = false;
        private readonly Dictionary<Connection, Task> connections_ = new();
        private readonly SandBox sandboxIO_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispacher_;

#nullable enable
        public Socket? Socket { get; private set; }
        public Action? OnInit { get; set; }
        public Action? OnDispose { get; set; }
#nullable disable

        public TcpServer(string ip, int port)
        {
            this.IP = ip;
            this.Port = port;

            msgDispacher_ = new Dispatcher();
            bus_ = new Bus(msgDispacher_);

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
                this.Socket.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                this.Socket.Close();
            }
        }

        private void IOHandler()
        {
            #region listen port
            try
            {
                Logger.Debug($"Start server {this.IP} {this.Port}");

                var ipa = IPAddress.Parse(this.IP);
                var ipe = new IPEndPoint(ipa, this.Port);

                this.Socket = new Socket(ipa.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.Socket.Bind(ipe);
                this.Socket.Listen(int.MaxValue);

                Logger.Debug("Listen succ");
            }
            catch (Exception)
            {
                throw;
            }
            #endregion

            this.OnInit?.Invoke();

            #region init bus pump task
            var busPumpTask = new Task(() =>
            {
                this.PumpMessageHandler();
            });
            busPumpTask.Start();
            #endregion

            #region message loop
            while (!stopFlag_)
            {
                Socket clientSocket;
                try {
                    clientSocket = this.Socket.Accept();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Socket closed.");
                    break;
                }

                var ipEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint;
                Logger.Debug($"New socket got {ipEndPoint.Address}:{ipEndPoint.Port}");

                var cancelTokenSource = new CancellationTokenSource();
                var conn = Connection.Create(clientSocket, cancelTokenSource);
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

        private void PumpMessageHandler()
        {
            try
            {
                while (!stopFlag_)
                {
                    this.bus_.Pump();
                    Thread.Sleep(30);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Pump message failed.");
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
            this.msgDispacher_.Register(key, callback);
        }

        public void UnregisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispacher_.Unregiser(key, callback);
        }

    }
}