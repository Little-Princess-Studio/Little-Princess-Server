using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.IPC;
using LPS.Core.RPC;

namespace LPS.Core
{
    /// <summary>
    /// Each gate need maintain multiple connections from remote clients
    /// and maintain a connection to hostmanager.
    /// For hostmanager, gate is a client
    /// for remote clients, gate is a server.
    /// All the gate mailbox info will be saved in redis, and gate will
    /// repeatly sync these info from redis.
    /// </summary>
    public class Gate
    {
        public readonly string name_;

        public MailBox MailBox { get; private set; }

        public string Name { get; private set; }

        private readonly string hostManagerIP_;
        private readonly  int hostManagerPort_;

        private readonly SandBox sandboxIOToHost_;
        
        private readonly SandBox sandBoxIOToClient_; 

        private bool stopFlag_ = false;

        private Socket socketToHostManager_;
        private Socket socketToClient_;

        private readonly List<Task> socketTasks_ = new ();

        private readonly Bus bus_ = new ();

        public Gate(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort)
        {
            this.Name = name;
            this.MailBox = new MailBox(ip, port, hostnum, -1);

            hostManagerIP_ = hostManagerIP;
            hostManagerPort_ = hostManagerPort;

            sandboxIOToHost_ = SandBox.Create(this.HostIOHandler);
            sandBoxIOToClient_ = SandBox.Create(this.GateIOHandler);
        }

        private void HostIOHandler()
        {
            try {
                var ipa = IPAddress.Parse(hostManagerIP_);
                var ipe = new IPEndPoint(ipa, hostManagerPort_);
                // todo: auto select net protocal later
                socketToHostManager_ = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socketToHostManager_.Connect(ipe);

                if (!socketToHostManager_.Connected)
                {
                    socketToHostManager_ = null;
                    var e = new Exception($"Target {this.MailBox} cannot be connected.");
                    Logger.Fatal(e, $"Target {this.MailBox} cannot be connected.");
                    throw e;
                }
            }
            catch (Exception)
            {
                throw;
            }

            // wait for message from hostmanager
            while (!stopFlag_)
            {
                Thread.Sleep(1);
            }
        }

        private void GateIOHandler()
        {
            try
            {
                Logger.Debug($"Start gate server {MailBox.IP} {MailBox.Port}");

                var ipa = IPAddress.Parse(MailBox.IP);
                var ipe = new IPEndPoint(ipa, MailBox.Port);

                socketToClient_ = new Socket(ipa.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                socketToClient_.Bind(ipe);
                socketToClient_.Listen(int.MaxValue);

                Logger.Debug("Listen succ");
            }
            catch (Exception)
            {
                throw;
            }

            while (!stopFlag_)
            {
                var clientSocket = socketToClient_.Accept();

                var ipEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint;
                
                Logger.Debug($"New socket got {ipEndPoint.Address}:{ipEndPoint.Port}");

                var task = new Task(() =>
                {
                    this.HandleGateMessage(clientSocket);
                });

                socketTasks_.Add(task);
                task.Start();
            }

            // wait each task end
            socketTasks_.ForEach(task => task.Wait());
        }

        private async void HandleGateMessage(Socket socket)
        {
            var buf = new byte[512];
            var seg = new ArraySegment<byte>(buf);
            try
            {
                while (!stopFlag_)
                {
                    var len = await socket.ReceiveAsync(seg, SocketFlags.None);

                    if (len < 1)
                    {
                        break;
                    }

                    // print recv msg here
                    Logger.Info($"got msg: {seg.Array}");
                }

                Logger.Debug("Connection Closed.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Read socket data failed");
                throw;
            }

            try
            {
                socket.Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Close socket failed");
                throw;
            }
        }

        public void Stop()
        {
            stopFlag_ = true;
        }

        public void Loop()
        {
            Logger.Debug($"Start gate at {this.MailBox.IP}:{this.MailBox.Port}");

            sandboxIOToHost_.Run();
            sandBoxIOToClient_.Run();

            // gate main thread will stuck here
            sandboxIOToHost_.WaitForExit();
            sandBoxIOToClient_.WaitForExit();
        }
    }
}
