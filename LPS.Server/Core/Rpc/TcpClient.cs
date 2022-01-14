using System;
using System.Collections.Concurrent;
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
    internal class TcpClient
    {
#nullable enable
        public Socket? Socket { get; private set; }
        public Action? OnInit { get; set; }
        public Action? OnDispose { get; set; }
        public Action? OnConnected { get; set; }
        public MailBox MailBox { get; set; }
#nullable disable
        private readonly SandBox sandboxIO_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispacher_;
        private readonly string targetIP_;
        private readonly int targetPort_;
        private readonly TokenSequence<uint> tokenSequence_ = new();
        private readonly ConcurrentQueue<(TcpClient, IMessage, bool)> sendQueue_;
        private bool stopFlag_;
        private const int ConnectRetryMaxTimes = 10;
        private uint idCounter_ = 0;

        public string TargetIP => targetIP_;
        public int TargetPort => targetPort_;

        public TcpClient(string targetIP, int targetPort, ConcurrentQueue<(TcpClient, IMessage, bool)> sendQueue)
        {
            targetIP_ = targetIP;
            targetPort_ = targetPort;
            sendQueue_ = sendQueue;

            msgDispacher_ = new Dispatcher();
            bus_ = new Bus(msgDispacher_);

            sandboxIO_ = SandBox.Create(this.IOHandler);
        }

        private async Task IOHandler()
        {
            try
            {
                this.OnInit?.Invoke();

                var ipa = IPAddress.Parse(targetIP_);
                var ipe = new IPEndPoint(ipa, targetPort_);
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
                this.OnDispose?.Invoke();
                throw;
            }

            Logger.Info("Connect to server succ.");
            OnConnected?.Invoke();

            var cancellationTokenSource = new CancellationTokenSource();
            var conn = Connection.Create(this.Socket, cancellationTokenSource);
            conn.Connect();

            while (!stopFlag_)
            {
                await this.HandleMessage(conn);
                Thread.Sleep(1);
            }

            cancellationTokenSource.Cancel();
            this.OnDispose?.Invoke();
        }

        private async Task HandleMessage(Connection conn)
        {
            await RpcHelper.HandleMessage(
                conn,
                () => stopFlag_,
                (msg) => bus_.AppendMessage(msg),
                null);
        }

        public uint GenerateMsgID() => idCounter_++;

        public void Send(IMessage msg, bool reentry=true)
        {
            try {
                sendQueue_.Enqueue((this, msg, reentry));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Send Error.");
            }
        }

        public void Pump() => this.bus_.Pump();

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

        public void RegisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispacher_.Register(key, callback);
        }

        public void UnregisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispacher_.Unregister(key, callback);
        }
    }
}
