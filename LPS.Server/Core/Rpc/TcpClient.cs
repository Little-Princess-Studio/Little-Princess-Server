using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Ipc;

namespace LPS.Core.Rpc
{
    internal class TcpClient
    {
#nullable enable
        public Socket? Socket { get; private set; }
        public Action? OnInit { get; set; }
        public Action? OnDispose { get; init; }
        public Action? OnConnected { get; init; }
        public MailBox MailBox { get; set; }
#nullable disable
        private readonly SandBox sandboxIo_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispatcher_;
        private readonly string targetIp_;
        private readonly int targetPort_;
        private readonly TokenSequence<uint> tokenSequence_ = new();
        private readonly ConcurrentQueue<(TcpClient, IMessage, bool)> sendQueue_;
        private bool stopFlag_;
        private const int ConnectRetryMaxTimes = 10;
        private uint idCounter_;
        public int TargetPort => targetPort_;
        public string TargetIp { get; set; }

        public TcpClient(string targetIp, int targetPort, ConcurrentQueue<(TcpClient, IMessage, bool)> sendQueue)
        {
            TargetIp = targetIp_ = targetIp;
            targetPort_ = targetPort;
            sendQueue_ = sendQueue;

            msgDispatcher_ = new Dispatcher();
            bus_ = new Bus(msgDispatcher_);

            sandboxIo_ = SandBox.Create(this.IoHandler);
        }

        private async Task IoHandler()
        {
            try
            {
                this.OnInit?.Invoke();

                var ipa = IPAddress.Parse(targetIp_);
                var ipe = new IPEndPoint(ipa, targetPort_);
                this.Socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                Logger.Debug($"Connect to: {targetIp_}:{targetPort_}");
                // repeat trying to connect
                int retryTimes = 0;
                while (!this.Socket.Connected && !stopFlag_ && retryTimes <= ConnectRetryMaxTimes)
                {
                    try
                    {
                        await this.Socket.ConnectAsync(ipe);
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
                    var e = new Exception($"Target {targetIp_} {targetPort_} cannot be connected.");
                    Logger.Fatal(e, $"Target {targetIp_} {targetPort_} cannot be connected.");
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

        public uint GenerateMsgId() => idCounter_++;

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
            this.sandboxIo_.Run();
        }

        public void WaitForExit()
        {
            this.sandboxIo_.WaitForExit();
        }

        public void Stop()
        {
            this.stopFlag_ = true;
            try
            {
                this.Socket!.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                this.Socket!.Close();
            }
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
