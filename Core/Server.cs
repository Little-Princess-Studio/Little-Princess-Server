using System;
using System.Collections.Generic;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

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

        private ServerEntity entity_;
        // private Dictionary<MailBox, BaseEntity> entitiesMap_ = new();
        private TcpServer tcpServer_;

        public Server(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort)
        {
            this.Name = name;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;

            tcpServer_ = new(ip, port);
            tcpServer_.OnInit = this.RegisterServerMessageHandlers;
            tcpServer_.OnDispose = this.UnregisterServerMessageHandlers;

            hostManagerIP_ = hostManagerIP;
            hostManagerPort_ = hostManagerPort;

            this.entity_ = new ServerEntity();
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
        }

        private void UnregisterServerMessageHandlers()
        {
            tcpServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.UnregisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
        }

        private void HandleEntityRpc(object arg)
        {
        }

        private void HandleCreateEntity(object arg)
        {
            (var msg, var _) = arg as Tuple<IMessage, Connection>;

            var createEntity = msg as CreateEntity;

            Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");
        }
    }
}