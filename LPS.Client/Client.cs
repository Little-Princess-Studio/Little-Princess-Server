using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Ipc;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Client
{
    public class Client
    {
        private string? ip_;
        private int port_;
        private Socket? socket_;

        public static readonly Client Instance = new ();
        private readonly SandBox ioSandBox_;
        private readonly SandBox sendSandBox_;
        private readonly SandBox pumpSandBox_;
        private readonly Bus bus_;
        private readonly Dispatcher msgDispatcher_;
        private bool exitFlag_;
        private readonly ConcurrentQueue<IMessage> sendQueue_ = new();
        private uint packageId_;

        private Client()
        {
            msgDispatcher_ = new Dispatcher();
            bus_ = new Bus(msgDispatcher_);
            
            ioSandBox_ = SandBox.Create(IoHandler);
            sendSandBox_ = SandBox.Create(SendHandler);
            pumpSandBox_ = SandBox.Create(PumpHandler);
        }

        public void RegisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispatcher_.Register(key, callback);
        }

        public void UnregisterMessageHandler(IComparable key, Action<object> callback)
        {
            this.msgDispatcher_.Unregister(key, callback);
        }

        private void PumpHandler()
        {
            while (!exitFlag_)
            {
                try
                {
                    bus_.Pump();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Pump message failed.");
                }
                finally
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void SendHandler()
        {
            while (!exitFlag_)
            {
                if (!sendQueue_.IsEmpty)
                {
                    var res = sendQueue_.TryDequeue(out var msg);
                    if (res)
                    {
                        var id = packageId_++;
                        var pkg = PackageHelper.FromProtoBuf(msg!, id);
                        var socket = this.socket_!;
                        try
                        {
                            socket.Send(pkg.ToBytes());
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Send msg failed.");
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }
        
        private async void IoHandler()
        {
            var ipa = IPAddress.Parse(ip_!);
            var ipe = new IPEndPoint(ipa, port_);
            
            // todo: auto select net protocol later
            socket_ = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Logger.Debug($"Connect to gate: {ip_}:{port_}");
            await socket_.ConnectAsync(ipe);

            if (!socket_.Connected)
            {
                socket_ = null;
                var e = new Exception($"Target cannot be connected.");
                Logger.Fatal(e, $"Target cannot be connected.");
                throw e;
            }
            
            Logger.Debug("Connected to Gate.");
            
            sendSandBox_.Run();
            pumpSandBox_.Run();

            var cancellationTokenSource = new CancellationTokenSource();
            var conn = Connection.Create(socket_, cancellationTokenSource);
            conn.Connect();

            while (!exitFlag_)
            {
                await this.HandleMessage(conn);
                Thread.Sleep(1);
            }
            
            cancellationTokenSource.Cancel();
        }

        private async Task HandleMessage(Connection conn)
        {
            await RpcHelper.HandleMessage(
                conn,
                () => exitFlag_,
                msg => bus_.AppendMessage(msg),
                null);
        }

        public void Init(string ip, int port)
        {
            ip_ = ip;
            port_ = port;            
        }
        
        public void Start()
        {
            ioSandBox_.Run();
        }

        public void WaitForExit()
        {
            this.sendSandBox_.WaitForExit();
            this.ioSandBox_.WaitForExit();
            this.pumpSandBox_.WaitForExit();
        }

        public void Stop()
        {
            exitFlag_ = true;
        }

        public void Send(IMessage msg)
        {
            try
            {
                sendQueue_.Enqueue(msg);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Send Error.");
            }
        }
    }
}
