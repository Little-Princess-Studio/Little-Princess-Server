using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using LPS.Core.Database;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Ipc;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using LPS.Logic.Entity;
using MailBox = LPS.Core.Rpc.MailBox;


/*
 * Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
 * redirect to target server instance. 
 */
namespace LPS.Core
{
    public class Server
    {
        public string Name { get; private set; }
        public string Ip { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        private readonly string hostManagerIP_;
        private readonly int hostManagerPort_;
        private ServerEntity? entity_;
        private CellEntity? defaultCell_;
        private readonly Dictionary<string, DistributeEntity> localEntityDict_ = new();
        private readonly Dictionary<string, CellEntity> cells_ = new();

        private readonly TcpServer tcpServer_;

        private Connection[] GateConnections => tcpServer_.AllConnections;

        private readonly CountdownEvent localEntityGeneratedEvent_;

        // private static readonly Random Random = new Random();

        public Server(string name, string ip, int port, int hostnum, string hostManagerIp, int hostManagerPort)
        {
            this.Name = name;
            this.Ip = ip;
            this.Port = port;
            this.HostNum = hostnum;

            localEntityGeneratedEvent_ = new(2);

            tcpServer_ = new TcpServer(ip, port)
            {
                OnInit = this.RegisterServerMessageHandlers,
                OnDispose = this.UnregisterServerMessageHandlers
            };

            hostManagerIP_ = hostManagerIp;
            hostManagerPort_ = hostManagerPort;

            // how server entity send msg
            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newId = task.Result;
                entity_ = new ServerEntity(new MailBox(newId, ip, port, hostnum))
                {
                    // todo: insert local rpc call operation to pump queue, instead of directly calling local entity rpc here.
                    OnSend = entityRpc => SendEntityRpc(entity_!, entityRpc),
                };

                Logger.Info("server entity generated.");
                localEntityGeneratedEvent_.Signal();
            });

            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newId = task.Result;
                defaultCell_ = new ServerDefaultCellEntity()
                {
                    MailBox = new MailBox(newId, ip, port, hostnum),
                    OnSend = entityRpc => SendEntityRpc(defaultCell_!, entityRpc),
                    EntityLeaveCallBack = entity => this.localEntityDict_.Remove(entity.MailBox.Id),
                    EntityEnterCallBack = (entity, gateMailBox) =>
                    {
                        entity.OnSend = entityRpc => SendEntityRpc(entity!, entityRpc);
                        if (entity is ServerClientEntity serverClientEntity)
                        {
                            Logger.Debug("transferred new serverClientEntity, bind new conn");
                            var gateConn = this.GateConnections.First(conn => conn.MailBox.CompareFull(gateMailBox));
                            serverClientEntity.BindGateConn(gateConn);
                        }

                        localEntityDict_.Add(entity.MailBox.Id, entity);
                    },
                };

                Logger.Info($"default cell generated, {defaultCell_.MailBox}.");
                // localEntityDict_.Add(newId, defaultCell_);
                cells_.Add(newId, defaultCell_);
                localEntityGeneratedEvent_.Signal();
            });
        }

        private void SendEntityRpc(BaseEntity baseEntity, EntityRpc entityRpc)
        {
            // send this rpc to gate
            var targetMailBox = entityRpc.EntityMailBox;

            // send to self
            if (baseEntity.MailBox.CompareFull(targetMailBox))
            {
                Logger.Info($"rpctype: {entityRpc.RpcType}");
                var rpcType = entityRpc.RpcType;
                if (rpcType == RpcType.ClientToServer || rpcType == RpcType.ServerInside)
                {
                    try
                    {
                        RpcHelper.CallLocalEntity(baseEntity, entityRpc);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception happened when call server entity");
                    }
                }
                else if (rpcType == RpcType.ServerToClient)
                {
                    var gateConn = (baseEntity as ServerClientEntity)!.Client.GateConn;

                    Logger.Info($"serverToClient rpc send to gate {gateConn.MailBox}");

                    tcpServer_.Send(entityRpc, gateConn);
                }
                else
                {
                    throw new Exception($"Invalid rpc type: {entityRpc.RpcType}");
                }
            }
            // send to local entity
            else if (localEntityDict_.ContainsKey(targetMailBox.ID))
            {
                var entity = localEntityDict_[targetMailBox.ID];

                try
                {
                    RpcHelper.CallLocalEntity(entity, entityRpc);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception happened when call local entity");
                }
            }
            else
            {
                // redirect to gate
                tcpServer_.Send(entityRpc, GateConnections[0]);
            }
        }

        private void OnCreateEntity(Connection gateConn, string entityClassName, string jsonDesc, MailBox mailBox)
        {
            var entity = RpcServerHelper.CreateEntityLocally(entityClassName, jsonDesc);

            Logger.Info($"Server create a new entity with mailbox {mailBox}");

            entity.SendSyncMessage = (keepOrder, delayTime, syncMsg) =>
            {
                Logger.Info($"Send sync msg {syncMsg.Operation} {syncMsg.MailBox} {syncMsg.RpcPropertyPath}"
                            + $"{syncMsg.RpcSyncPropertyType}:{delayTime}:{keepOrder}");
                tcpServer_.AddMessageToTimeCircle(syncMsg, delayTime, keepOrder);
            };

            if (entity is ServerClientEntity serverClientEntity)
            {
                // bind gate conn to client entity
                serverClientEntity.BindGateConn(gateConn);
            }

            entity.OnSend = entityRpc => SendEntityRpc(entity, entityRpc);
            entity.MailBox = mailBox;
            
            localEntityDict_[mailBox.Id] = entity;

            defaultCell_!.ManualyAdd(entity);
        }

        public void Stop()
        {
            tcpServer_.Stop();
        }

        public void Loop()
        {
            localEntityGeneratedEvent_.Wait();
            Logger.Debug($"Start gate at {this.Ip}:{this.Port}");
            tcpServer_.Run();

            // gate main thread will stuck here
            tcpServer_.WaitForExit();
        }

        private void RegisterServerMessageHandlers()
        {
            tcpServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.RegisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
            tcpServer_.RegisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
            tcpServer_.RegisterMessageHandler(PackageType.RequirePropertyFullSync, this.HandleRequirePropertyFullSync);
            tcpServer_.RegisterMessageHandler(PackageType.PropertyFullSyncAck, this.HandlePropertyFullSyncAck);
        }

        private void UnregisterServerMessageHandlers()
        {
            tcpServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.UnregisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
            tcpServer_.UnregisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
            tcpServer_.UnregisterMessageHandler(PackageType.RequirePropertyFullSync, this.HandleRequirePropertyFullSync);
            tcpServer_.UnregisterMessageHandler(PackageType.PropertyFullSyncAck, this.HandlePropertyFullSyncAck);
        }

        // how server handle entity rpc
        private void HandleEntityRpc(object arg)
        {
            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg;
            var entityRpc = (msg as EntityRpc)!;

            var targetMailBox = entityRpc.EntityMailBox;

            if (entity_!.MailBox!.CompareFull(targetMailBox))
            {
                Logger.Debug($"Call server entity: {entityRpc.MethodName}");
                try
                {
                    RpcHelper.CallLocalEntity(entity_, entityRpc);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception happend when call server entity");
                }
            }
            else if (cells_.ContainsKey(targetMailBox.ID))
            {
                var cell = cells_[targetMailBox.ID];
                try
                {
                    RpcHelper.CallLocalEntity(cell, entityRpc);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception happened when call cell");
                }
            }
            else if (localEntityDict_.ContainsKey(targetMailBox.ID))
            {
                var entity = localEntityDict_[targetMailBox.ID];
                try
                {
                    RpcHelper.CallLocalEntity(entity, entityRpc);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception happened when call server distribute entity");
                }
            }
            else
            {
                // redirect to gate
                tcpServer_.Send(entityRpc, GateConnections[0]);
            }
        }

        private void HandleCreateEntity(object arg)
        {
            var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;

            var createEntity = (msg as CreateEntity)!;

            Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

            // todo: move this quest to hostmanager
            switch (createEntity.CreateType)
            {
                case CreateType.Local:
                    break;
                case CreateType.Anywhere:
                    CreateAnywhereEntity(createEntity, id, conn);
                    break;
                case CreateType.Manual:
                    CreateManualEntity(createEntity, id, conn);
                    break;
            }
        }

        private void CreateAnywhereEntity(CreateEntity createEntity, uint id, Connection conn)
        {
            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newId = task.Result;

                var entityClassName = createEntity.EntityClassName!;
                var jsonDesc = createEntity.Description!;
                var entityMailBox = new MailBox(newId, this.Ip, this.Port, this.HostNum);

                OnCreateEntity(conn, entityClassName, jsonDesc, entityMailBox);

                var createEntityRes = new CreateEntityRes
                {
                    Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entityMailBox),
                    EntityType = createEntity.EntityType,
                    ConnectionID = createEntity.ConnectionID,
                    EntityClassName = createEntity.EntityClassName
                };
                
                Logger.Debug("Create Entity Anywhere");
                var pkg = PackageHelper.FromProtoBuf(createEntityRes, id);
                conn.Socket.Send(pkg.ToBytes());
            });
        }

        private void CreateManualEntity(CreateEntity createEntity, uint id, Connection conn)
        {
            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newId = task.Result;
                var entityMailBox = new CreateEntityRes
                {
                    Mailbox = new Rpc.InnerMessages.MailBox
                    {
                        IP = "",
                        Port = 0,
                        HostNum = (uint) this.HostNum,
                        ID = newId
                    },
                    EntityType = createEntity.EntityType,
                    ConnectionID = createEntity.ConnectionID,
                    EntityClassName = createEntity.EntityClassName
                };
                var pkg = PackageHelper.FromProtoBuf(entityMailBox, id);
                conn.Socket.Send(pkg.ToBytes());
            });
        }

        private void HandleExchangeMailBox(object arg)
        {
            var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;

            var gateMailBox = (msg as ExchangeMailBox)!.Mailbox;
            var socket = conn.Socket;

            Logger.Info(
                $"exchange mailbox: {gateMailBox.ID} {gateMailBox.IP} {gateMailBox.Port} {gateMailBox.HostNum}");

            conn.MailBox = RpcHelper.PbMailBoxToRpcMailBox(gateMailBox);

            var res = new ExchangeMailBoxRes
            {
                Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entity_!.MailBox),
            };

            var pkg = PackageHelper.FromProtoBuf(res, id);
            socket.Send(pkg.ToBytes());
        }

        private void HandleRequirePropertyFullSync(object arg)
        {
            Logger.Debug("HandleRequirePropertyFullSync");
            var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;
            var requirePropertyFullSyncMsg = (msg as RequirePropertyFullSync)!;
            var entityId = requirePropertyFullSyncMsg.EntityId;

            if (localEntityDict_.ContainsKey(entityId))
            {
                Logger.Debug("Prepare for full sync");
                var entity = localEntityDict_[entityId];
                entity.FullSync(((_, content) =>
                {
                    Logger.Debug("Full sync send back");

                    var fullSync = new PropertyFullSync
                    {
                        EntityId = entityId,
                        PropertyTree = content,
                    };
                    // conn.Socket.SendAsync(fullSync.ToByteArray(), SocketFlags.None);
                    var pkg = PackageHelper.FromProtoBuf(fullSync, id);
                    conn.Socket.Send(pkg.ToBytes());
                }));
            }
            else
            {
                throw new Exception($"Entity not exist: {entityId}");
            }
        }

        private void HandlePropertyFullSyncAck(object arg)
        {
            var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;
            var propertyFullSyncAckMsg = (msg as PropertyFullSyncAck)!;
            var entityId = propertyFullSyncAckMsg.EntityId;

            if (localEntityDict_.ContainsKey(entityId))
            {
                var entity = localEntityDict_[entityId];
                entity.FullSyncAck();
            }
            else
            {
                throw new Exception($"Entity not exist: {entityId}");
            }
        }
    }
}