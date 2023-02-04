using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Debug;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Server.Core.Database;
using LPS.Server.Core.Rpc;
using LPS.Server.Core.Rpc.InnerMessages;
using MailBox = LPS.Common.Core.Rpc.MailBox;

/*
 *         HostMgr
 * |----------|----------|
 * Server0 Server1 Server2
 * |----------|----------|
 * Gate0             Gate1
 * |---------|         |
 * Client0 Client1   Client2
 */

/*
 * Host start step
 * 1. HostManager starts, set host status to State0
 * 2. Each component start&init themselves and retry to connect to HostManager
 * 3. After connected, each component will send a signal (RegisterInstance)
 *    to host manager register themselves and wait for the register success response
 * 4. HostManager record the registered components, if all the components in config are registered
 *    Set host status to State1
 * 5. HostManager send success response to each components
 * 6. After all register succ messages are sent, HostManager will broadcast sync message to all
 *    the components to let them sync connection. When a component receives the sync message
 *    and complete all the connecting actions, it will send sync success message to HostManager
 * 7. HostManager records all the sync success messages, if all the components have sent messages
 *    HostManager will set host status to State2
 * 8. HostManager will broadcast Open message to all the gate instances to let them allow connections
 *    from clients.
 */
namespace LPS.Server.Core
{
    public enum HostStatus
    {
        None, // init
        State0,
        State1,
        State2,
        State3, // stopping
        State4, // stopped
    }

    /// <summary>
    /// HostManager will watch the status of each component in the host including:
    /// Server/Gate/DbManager
    /// HostManager use ping/pong strategy to check the status of the components
    /// if HostManager find any component looks like dead, it will
    /// kick this component off from the host, try to create a new component
    /// while writing alert log. 
    /// </summary>
    public class HostManager : IInstance
    {
        public InstanceType InstanceType => InstanceType.HostManager;

        public readonly string Name;
        public readonly int HostNum;
        public readonly string Ip;
        public readonly int Port;
        public readonly int ServerNum;
        public readonly int GateNum;

        private readonly TcpServer tcpServer_;
        public List<Connection> ServersConn { get; private set; } = new List<Connection>();
        public List<Connection> GatesConn { get; private set; } = new List<Connection>();

        public HostStatus Status { get; private set; } = HostStatus.None;

        private readonly Random random_ = new();
        private uint createEntityCnt_;

        private Dictionary<uint, (uint ConnId, Connection OriginConn, string EntityClassName)>
            createDistEntityAsyncRecord_ = new();

        public HostManager(string name, int hostNum, string ip, int port, int serverNum, int gateNum)
        {
            this.Name = name;
            this.HostNum = hostNum;
            this.Ip = ip;
            this.Port = port;
            this.ServerNum = serverNum;
            this.GateNum = gateNum;

            tcpServer_ = new TcpServer(ip, port)
            {
                OnInit = this.RegisterServerMessageHandlers,
                OnDispose = this.UnregisterServerMessageHandlers,
                ServerTickHandler = null,
            };
        }

        private void UnregisterServerMessageHandlers()
        {
            tcpServer_.UnregisterMessageHandler(PackageType.Control, this.HandleControlCmd);
            tcpServer_.UnregisterMessageHandler(PackageType.RequireCreateEntity, this.HandleControlCmd);
            tcpServer_.UnregisterMessageHandler(PackageType.CreateDistributeEntityRes,
                this.HandleCreateDistributeEntityRes);
        }

        private void RegisterServerMessageHandlers()
        {
            tcpServer_.RegisterMessageHandler(PackageType.Control, this.HandleControlCmd);
            tcpServer_.RegisterMessageHandler(PackageType.RequireCreateEntity, this.HandleRequireCreateEntity);
            tcpServer_.RegisterMessageHandler(PackageType.CreateDistributeEntityRes,
                this.HandleCreateDistributeEntityRes);
        }

        private void HandleCreateDistributeEntityRes(object arg)
        {
            var (msg, _, id) = ((IMessage, Connection, UInt32)) arg;
            var createRes = (msg as CreateDistributeEntityRes)!;

            if (!createDistEntityAsyncRecord_.ContainsKey(createRes.ConnectionID))
            {
                Logger.Warn($"Key {createRes.ConnectionID} not in the record.");
                return;
            }

            var (oriConnId, conn, entityClassName) = createDistEntityAsyncRecord_[createRes.ConnectionID];

            var requireCreateRes = new RequireCreateEntityRes
            {
                Mailbox = createRes.Mailbox,
                ConnectionID = oriConnId,
                EntityType = EntityType.DistibuteEntity,
                EntityClassName = entityClassName,
            };

            var pkg = PackageHelper.FromProtoBuf(requireCreateRes, id);
            conn.Socket.Send(pkg.ToBytes());
        }

        private void HandleRequireCreateEntity(object arg)
        {
            var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;
            var createEntity = (msg as RequireCreateEntity)!;

            Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

            switch (createEntity.CreateType)
            {
                case CreateType.Local:
                    CreateLocalEntity(createEntity, id, conn);
                    break;
                case CreateType.Anywhere:
                    CreateAnywhereEntity(createEntity, id, conn);
                    break;
                case CreateType.Manual:
                    CreateManualEntity(createEntity, id, conn);
                    break;
            }
        }

        private void CreateLocalEntity(RequireCreateEntity createEntity, uint id, Connection conn) =>
            CreateManualEntity(createEntity, id, conn);

        private void CreateManualEntity(RequireCreateEntity createEntity, uint id, Connection conn)
        {
            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newId = task.Result;
                var entityMailBox = new RequireCreateEntityRes
                {
                    Mailbox = new LPS.Common.Core.Rpc.InnerMessages.MailBox
                    {
                        IP = "",
                        Port = 0,
                        HostNum = (uint) this.HostNum,
                        ID = newId
                    },
                    EntityType = createEntity.EntityType,
                    ConnectionID = createEntity.ConnectionID,
                    EntityClassName = createEntity.EntityClassName,
                };
                var pkg = PackageHelper.FromProtoBuf(entityMailBox, id);
                conn.Socket.Send(pkg.ToBytes());
            });
        }

        /// <summary>
        ///server craete eneity anywhere step:
        /// 1. Server (origin server) sends RequireCreateEntity msg to host manager
        /// 2. HostManager randomly selects a server and send CreateEntity msg
        /// 3. Selected server creates entity and sends CreateEntityRes to host manager
        /// 4. Host manager sends CreateEntityRes to origin server
        /// </summary>
        /// <param name="createEntity"></param>
        /// <param name="id"></param>
        /// <param name="conn"></param>
        private void CreateAnywhereEntity(RequireCreateEntity createEntity, uint id, Connection conn)
        {
            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newId = task.Result;
                Logger.Debug("Randomly select a server");
                var serverConn = ServersConn[random_.Next(0, ServersConn.Count)];

                Logger.Debug("Create Entity Anywhere");
                var connId = createEntityCnt_++;
                var createDist = new CreateDistributeEntity
                {
                    EntityClassName = createEntity.EntityClassName,
                    Description = createEntity.Description,
                    ConnectionID = connId,
                    EntityId = newId,
                };
                var pkg = PackageHelper.FromProtoBuf(createDist, id);
                serverConn.Socket.Send(pkg.ToBytes());
                // record
                createDistEntityAsyncRecord_[connId] = (createEntity.ConnectionID, conn, createEntity.EntityClassName);
            });
        }

        private void HandleControlCmd(object arg)
        {
            var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg;
            var hostCmd = (msg as Control)!;
            switch (hostCmd.Message)
            {
                case ControlMessage.Ready:
                    this.RegisterComponents(hostCmd.From,
                        RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                            .Unpack<LPS.Common.Core.Rpc.InnerMessages.MailBox>()), conn);
                    break;
                case ControlMessage.Restart:
                    break;
                case ControlMessage.ShutDown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void RegisterComponents(RemoteType hostCmdFrom, MailBox mailBox, Connection conn)
        {
            conn.MailBox = mailBox;
            
            if (hostCmdFrom == RemoteType.Gate)
            {
                Logger.Info($"gate require sync {mailBox}");
                this.GatesConn.Add(conn);
                if (GatesConn.Count == this.GateNum)
                {
                    Logger.Info("All gates registered, send sync msg");

                    // broadcast sync msg
                    var syncCmd = new HostCommand
                    {
                        Type = HostCommandType.SyncGates
                    };

                    foreach (var gateConn in this.GatesConn)
                    {
                        syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(gateConn.MailBox)));
                    }

                    // send gates mailboxes
                    var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
                    foreach (var gateConn in this.GatesConn)
                    {
                        gateConn.Socket.Send(pkg.ToBytes());
                    }
                    
                    syncCmd = new HostCommand
                    {
                        Type = HostCommandType.SyncServers
                    };
                    // send server mailboxes
                    foreach (var serverConn in this.ServersConn)
                    {
                        syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(serverConn.MailBox)));
                    }

                    pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
                    foreach (var gateConn in this.GatesConn)
                    {
                        gateConn.Socket.Send(pkg.ToBytes());
                    }
                }
            }
            else if (hostCmdFrom == RemoteType.Server)
            {
                Logger.Info($"server require sync {mailBox}");
                this.ServersConn.Add(conn);
                if (ServersConn.Count == this.ServerNum)
                {
                    Logger.Info("All servers registered, send sync msg");
                    
                    // broadcast sync msg
                    // var syncCmd = new HostCommand
                    // {
                    //     Type = HostCommandType.SyncServers
                    // };
                    //
                    // foreach (var serverConn in this.ServersConn)
                    // {
                    //     syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(serverConn.MailBox)));
                    // }
                    //
                    // var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
                    // foreach (var serverConn in this.ServersConn)
                    // {
                    //     serverConn.Socket.Send(pkg.ToBytes());
                    // }
                }
            }
        }

        public void Loop()
        {
            Logger.Debug($"Start Host Manager at {this.Ip}:{this.Port}");
            tcpServer_.Run();
            tcpServer_.WaitForExit();
        }

        public void Stop()
        {
            tcpServer_.Stop();
        }
    }
}