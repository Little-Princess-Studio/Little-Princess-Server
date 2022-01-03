using System;
using System.Linq;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;


/*
 * Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
 * redirect to target server instance. 
 */
namespace LPS.Core
{
    public class Server
    {
        public string Name { get; private set; }
        public string IP { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        private readonly string hostManagerIP_;
        private readonly int hostManagerPort_;

        private readonly ServerEntity entity_;

        // private Dictionary<MailBox, BaseEntity> entitiesMap_ = new();
        private readonly TcpServer tcpServer_;

        private Connection[] GateConnections => tcpServer_.AllConnections;

        private static readonly Random Random = new Random();

        public Server(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort)
        {
            this.Name = name;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;

            tcpServer_ = new(ip, port)
            {
                OnInit = this.RegisterServerMessageHandlers,
                OnDispose = this.UnregisterServerMessageHandlers
            };

            hostManagerIP_ = hostManagerIP;
            hostManagerPort_ = hostManagerPort;

            entity_ = new(new Rpc.MailBox(RandomString(16), ip, port, hostnum),
                entityRpc =>
                {
                    // send this rpc to gate
                    var targetMailBox = entityRpc.EntityMailBox;

                    if (targetMailBox.ID == this.entity_!.MailBox!.ID
                        && targetMailBox.IP == this.entity_.MailBox.IP
                        && targetMailBox.Port == this.entity_.MailBox.Port
                        && targetMailBox.HostNum == this.entity_.MailBox.HostNum)
                    {
                        RpcHelper.CallLocalEntity(this.entity_, entityRpc);
                    }
                    // todo: call local entity
                    else
                    {
                        // redirect to gate
                        this.tcpServer_.Send(entityRpc, GateConnections[0]);
                    }
                });
        }
        
        public void Stop()
        {
            this.tcpServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start gate at {this.IP}:{this.Port}");
            this.tcpServer_.Run();

            // gate main thread will stuck here
            this.tcpServer_.WaitForExit();
        }

        private void RegisterServerMessageHandlers()
        {
            tcpServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.RegisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
            tcpServer_.RegisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
        }

        private void UnregisterServerMessageHandlers()
        {
            tcpServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.UnregisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
            tcpServer_.UnregisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
        }

        private void HandleEntityRpc(object arg)
        {
            var (msg, conn, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var entityRpc = (msg as EntityRpc)!;

            var targetMailBox = entityRpc.EntityMailBox;

            if (targetMailBox.ID == this.entity_!.MailBox!.ID
                && targetMailBox.IP == this.entity_.MailBox.IP
                && targetMailBox.Port == this.entity_.MailBox.Port
                && targetMailBox.HostNum == this.entity_.MailBox.HostNum)
            {
                Logger.Debug($"Call server entity: {entityRpc.MethodName}");
                RpcHelper.CallLocalEntity(this.entity_, entityRpc);
            }
            // todo: call local entity
            else
            {
                // redirect to gate
                this.tcpServer_.Send(entityRpc, GateConnections[0]);
            }
        }

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
        }

        private void HandleCreateEntity(object arg)
        {
            var (msg, conn, id) = (arg as Tuple<IMessage, Connection, UInt32>)!;

            var createEntity = (msg as CreateEntity)!;
            var socket = conn.Socket;

            Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

            // todo: move this quest to hostmanager
            switch (createEntity.CreateType)
            {
                case CreateType.Local:
                    break;
                case CreateType.Anywhere:
                    break;
                case CreateType.Manual:
                    var newID = RandomString(16);
                    var entityMailBox = new CreateEntityRes
                    {
                        Mailbox = new Rpc.InnerMessages.MailBox()
                        {
                            IP = "",
                            Port = 0,
                            HostNum = (uint) this.HostNum,
                            ID = newID,
                        }
                    };
                    var pkg = PackageHelper.FromProtoBuf(entityMailBox, id);
                    socket.Send(pkg.ToBytes());
                    break;
            }
        }

        private void HandleExchangeMailBox(object arg)
        {
            var (msg, conn, id) = (arg as Tuple<IMessage, Connection, UInt32>)!;

            var gateMailBox = (msg as ExchangeMailBox)!.Mailbox;
            var socket = conn.Socket;

            Logger.Info(
                $"exchange mailbox: {gateMailBox.ID} {gateMailBox.IP} {gateMailBox.Port} {gateMailBox.HostNum}");

            conn.MailBox = RpcHelper.PbMailBoxToRpcMailBox(gateMailBox);

            var res = new ExchangeMailBoxRes()
            {
                Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entity_.MailBox!),
            };

            var pkg = PackageHelper.FromProtoBuf(res, id);
            socket.Send(pkg.ToBytes());
        }
    }
}