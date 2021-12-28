using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Ipc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc
{
    internal class TcpClient
    {
#nullable enable
        public Socket? Socket { get; private set; }
        public Action? OnInit { get; set; }
        public Action? OnDispose { get; set; }
#nullable disable
        private readonly SandBox sandboxIO_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispacher_;
        private readonly string targetIP_;
        private readonly int targetPort_;
        private bool stopFlag_;

        private const int ConnectRetryMaxTimes = 10;

        public TcpClient(string targetIP, int targetPort)
        {
            targetIP_ = targetIP;
            targetPort_ = targetPort;

            msgDispacher_ = new Dispatcher();
            bus_ = new Bus(msgDispacher_);

            sandboxIO_ = SandBox.Create(this.IOHandler);
        }

        private void IOHandler()
        {
            try
            {
                var ipa = IPAddress.Parse(targetIP_);
                var ipe = new IPEndPoint(ipa, targetPort_);
                // todo: auto select net protocal later
                this.Socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                Logger.Debug($"Connect to: {targetIP_}:{targetPort_}");

                // repeat trying to connect

                int retryTimes = 0;
                while (!this.Socket.Connected && !stopFlag_ && retryTimes <= ConnectRetryMaxTimes)
                {
                    try
                    {
                        this.Socket.Connect(ipe);
                    }
                    catch (Exception e)
                    {
                        ++retryTimes;
                        Logger.Error(e, $"Connect failed, retry for {retryTimes} times...");
                        Thread.Sleep(1000);
                    }
                }

                if (!this.Socket.Connected)
                {
                    this.Socket = null;
                    var e = new Exception($"Target {targetIP_} {targetPort_} cannot be connected.");
                    Logger.Fatal(e, $"Target {targetIP_} {targetPort_} cannot be connected.");
                    throw e;
                }
            }
            catch (Exception)
            {
                throw;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var conn = Connection.Create(this.Socket, cancellationTokenSource);
            conn.Connect();

            while (!stopFlag_)
            {
                this.HandleMessage(conn);
                Thread.Sleep(30);
            }
        }

        private async void HandleMessage(Connection conn)
        {
            await RpcHelper.HandleMessage(
                conn,
                () => stopFlag_,
                (msg) => bus_.AppendMessage(msg),
                null);
        }

        public void Send<T>(T msg) where T : IMessage<T>, new()
        {
            try {
                var pkg = Package.FromProtoBuf(msg);
                this.Socket.Send(pkg.ToBytes());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Send Error.");
            }
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
    }
}