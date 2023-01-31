using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using LPS.Common.Core.Debug;
using LPS.Common.Core.Entity;
using LPS.Common.Core.Ipc;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcPropertySync;
using LPS.Server.Core.Database;
using LPS.Server.Core.Entity;
using LPS.Server.Core.Rpc;
using LPS.Server.Core.Rpc.InnerMessages;
using MailBox = LPS.Common.Core.Rpc.MailBox;


/*
 * Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
 * redirect to target server instance. 
 */
namespace LPS.Server.Core
{
    public class Server : IInstance
    {
        public readonly string Name;
        public readonly string Ip;
        public readonly int Port;
        public readonly int HostNum;
        
        public InstanceType InstanceType => InstanceType.Server;

        private ServerEntity? entity_;
        private CellEntity? defaultCell_;
        private readonly Dictionary<string, DistributeEntity> localEntityDict_ = new();
        private readonly Dictionary<string, CellEntity> cells_ = new();

        private Connection[] GateConnections => tcpServer_.AllConnections;
        private readonly CountdownEvent localEntityGeneratedEvent_;
        
        private readonly ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)> timeCircleQueue_ = new();
        // todo: use constant value to init time circle
        private readonly TimeCircle timeCircle_ = new(50, 1000);
        private readonly Random random_ = new();

        private readonly TcpServer tcpServer_;
        private readonly TcpClient clientToHostManager_;

        private uint createEntityCounter_;

        private CountdownEvent hostManagerConnectedEvent_;
        private CountdownEvent waitForSyncEvent_;
        private readonly SandBox clientsPumpMsgSandBox_;

        public Server(string name, string ip, int port, int hostnum, string hostManagerIp, int hostManagerPort)
        {
            this.Name = name;
            this.Ip = ip;
            this.Port = port;
            this.HostNum = hostnum;
            
            tcpServer_ = new TcpServer(ip, port)
            {
                OnInit = this.RegisterServerMessageHandlers,
                OnDispose = this.UnregisterServerMessageHandlers,
                ServerTickHandler = this.OnTick,
            };

            timeCircle_.Start();

            localEntityGeneratedEvent_ = new(2);
            hostManagerConnectedEvent_ = new(1);
            clientToHostManager_ = new TcpClient(hostManagerIp, hostManagerPort, new ConcurrentQueue<(TcpClient, IMessage, bool)>())
            {
                OnInit = () =>
                {
                    clientToHostManager_!.RegisterMessageHandler(PackageType.RequireCreateEntityRes, this.HandleRequireCreateEntityResFromHost);

                    clientToHostManager_!.Send(new RequireCreateEntity
                    {
                        EntityType = EntityType.ServerEntity,
                        CreateType = CreateType.Manual,
                        EntityClassName = "",
                        Description = "",
                        ConnectionID = createEntityCounter_++
                    });

                    clientToHostManager_!.Send(new RequireCreateEntity
                    {
                        EntityType = EntityType.ServerDefaultCellEntity,
                        CreateType = CreateType.Manual,
                        EntityClassName = "",
                        Description = "",
                        ConnectionID = createEntityCounter_++
                    });
                },
                OnDispose = () => clientToHostManager_!.UnregisterMessageHandler(PackageType.RequireCreateEntityRes, this.HandleRequireCreateEntityResFromHost),
                OnConnected = () => hostManagerConnectedEvent_.Signal(1)
            };
            
            clientsPumpMsgSandBox_ = SandBox.Create(this.PumpMessageHandler);
        }

        private void OnTick(uint deltaTime)
        {
            timeCircle_.Tick(deltaTime, command =>
            {
                var entityId = command.EntityId;
                var entity = this.localEntityDict_[entityId];
                Connection gateConn;
                
                // todo: handle sync to local shadow entity
                if (entity is ServerClientEntity serverClientEntity)
                {
                    gateConn = serverClientEntity.Client.GateConn;
                }
                else
                {
                    gateConn = this.GateConnections[random_.Next(0, this.GateConnections.Length)];
                }
                
                tcpServer_.Send(command, gateConn);
                Logger.Debug($"[Dispatch Prop Sync msg]: {command} to {gateConn.MailBox}");
            });
        }
        
        private void TimeCircleSyncMessageEnqueueHandler()
        {
            while (!tcpServer_.Stopped)
            {
                while (!timeCircleQueue_.IsEmpty)
                {
                    var res = timeCircleQueue_.TryDequeue(out var tp);
                    if (res)
                    {
                        Logger.Debug("Time circle not empty, pump message");
                        var (keepOrder, delayTime, msg) = tp;
                        timeCircle_.AddPropertySyncMessage(msg, delayTime, keepOrder);
                    }
                }
                Thread.Sleep(1);
            }
        }
        
        public void AddMessageToTimeCircle(RpcPropertySyncMessage msg, uint delayTimeByMilliseconds, bool keepOrder)
            => timeCircleQueue_.Enqueue((keepOrder, delayTimeByMilliseconds, msg));
        
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

            entity.SendSyncMessageHandler = (keepOrder, delayTime, syncMsg) =>
            {
                Logger.Info($"Send sync msg {syncMsg.Operation} {syncMsg.MailBox} {syncMsg.RpcPropertyPath}"
                            + $"{syncMsg.RpcSyncPropertyType}:{delayTime}:{keepOrder}");
                this.AddMessageToTimeCircle(syncMsg, delayTime, keepOrder);
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
            clientToHostManager_.Stop();
            tcpServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start server at {this.Ip}:{this.Port}");
            tcpServer_.Run();
            clientToHostManager_.Run();
            clientsPumpMsgSandBox_.Run();

            Logger.Debug($"Start time circle pump.");
            var sendQueueSandBox = SandBox.Create(this.TimeCircleSyncMessageEnqueueHandler);
            sendQueueSandBox.Run();

            hostManagerConnectedEvent_.Wait();
            Logger.Debug("Host manager connected.");
            
            localEntityGeneratedEvent_.Wait();
            Logger.Debug($"Local entity generated. {entity_!.MailBox}");

            // register server and wait for sync ack
            var regCtl = new Control
            {
                From = RemoteType.Server,
                Message = ControlMessage.Ready,
            };
            clientToHostManager_.Send(regCtl);
            
            // gate main thread will stuck here
            clientToHostManager_.WaitForExit();
            tcpServer_.WaitForExit();
            clientsPumpMsgSandBox_.WaitForExit();
        }

        private void RegisterServerMessageHandlers()
        {
            tcpServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            // tcpServer_.RegisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
            tcpServer_.RegisterMessageHandler(PackageType.RequirePropertyFullSync, this.HandleRequirePropertyFullSync);
            tcpServer_.RegisterMessageHandler(PackageType.PropertyFullSyncAck, this.HandlePropertyFullSyncAck);
            tcpServer_.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
            tcpServer_.RegisterMessageHandler(PackageType.CreateDistributeEntity, this.HandleCreateDistributeEntity);
        }
        
        private void UnregisterServerMessageHandlers()
        {
            tcpServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            // tcpServer_.UnregisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
            tcpServer_.UnregisterMessageHandler(PackageType.RequirePropertyFullSync, this.HandleRequirePropertyFullSync);
            tcpServer_.UnregisterMessageHandler(PackageType.PropertyFullSyncAck, this.HandlePropertyFullSyncAck);
            tcpServer_.UnregisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
            tcpServer_.UnregisterMessageHandler(PackageType.CreateDistributeEntity, this.HandleCreateDistributeEntity);
        }
        
        private void HandleCreateDistributeEntity(object arg)
        {
            var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;
            var createDist = (msg as CreateDistributeEntity)!;
            
            var newId = createDist.EntityId!;
            var entityClassName = createDist.EntityClassName!;
            var jsonDesc = createDist.Description!;

            var entityMailBox = new MailBox(newId, this.Ip, this.Port, this.HostNum);
            
            OnCreateEntity(conn, entityClassName, jsonDesc, entityMailBox);
            
            var createEntityRes = new CreateDistributeEntityRes
            {
                Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entityMailBox),
                ConnectionID = createDist.ConnectionID
            };
            
            Logger.Debug("Create Entity Anywhere");
            var pkg = PackageHelper.FromProtoBuf(createEntityRes, id);
            conn.Socket.Send(pkg.ToBytes());
        }
        
        private void HandleRequireCreateEntityResFromHost(object arg)
        {
            var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg;
            var createRes = (msg as RequireCreateEntityRes)!;
            
            Logger.Info($"Create Entity Res: {createRes.EntityType} {createRes.ConnectionID}");
            
            switch (createRes.EntityType)
            {
                case EntityType.ServerEntity:
                    CreateServerEntity(createRes);
                    break;
                case EntityType.ServerDefaultCellEntity:
                    CreateServerDefaultCellEntity(createRes);
                    break;
                case EntityType.ServerClientEntity:
                case EntityType.GateEntity:
                case EntityType.DistibuteEntity:
                default:
                    Logger.Warn($"Invalid Create Entity Res Type: {createRes.EntityType}");
                    break;
            }
        }

        private void CreateServerDefaultCellEntity(RequireCreateEntityRes createRes)
        {
            var newId = createRes.Mailbox.ID;
            defaultCell_ = new ServerDefaultCellEntity()
            {
                MailBox = new MailBox(newId, this.Ip, this.Port, this.HostNum),
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
            cells_.Add(newId, defaultCell_);

            localEntityGeneratedEvent_.Signal(1);
        }

        private void CreateServerEntity(RequireCreateEntityRes createRes)
        {
            var serverEntityMailBox =
                new MailBox(createRes.Mailbox.ID, this.Ip, this.Port, this.HostNum);
            entity_ = new ServerEntity(serverEntityMailBox)
            {
                // todo: insert local rpc call operation to pump queue, instead of directly calling local entity rpc here.
                OnSend = entityRpc => SendEntityRpc(entity_!, entityRpc),
            };

            Logger.Info("server entity generated.");
            
            localEntityGeneratedEvent_.Signal(1);
        }

        private void HandleHostCommand(object arg)
        {
            var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg;
            var hostCmd = (msg as HostCommand)!;
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
        
        // private void HandleExchangeMailBox(object arg)
        // {
        //     var (msg, conn, id) = ((IMessage, Connection, UInt32)) arg;
        //
        //     var gateMailBox = (msg as ExchangeMailBox)!.Mailbox;
        //     var socket = conn.Socket;
        //
        //     Logger.Info(
        //         $"exchange mailbox: {gateMailBox.ID} {gateMailBox.IP} {gateMailBox.Port} {gateMailBox.HostNum}");
        //
        //     conn.MailBox = RpcHelper.PbMailBoxToRpcMailBox(gateMailBox);
        //
        //     var res = new ExchangeMailBoxRes
        //     {
        //         Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entity_!.MailBox),
        //     };
        //
        //     var pkg = PackageHelper.FromProtoBuf(res, id);
        //     socket.Send(pkg.ToBytes());
        // }

        private void PumpMessageHandler()
        {
            try
            {
                while (!tcpServer_.Stopped)
                {
                    clientToHostManager_.Pump();
                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Pump message failed.");
            }
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
                Logger.Info("Full sync ack succ");
            }
            else
            {
                throw new Exception($"Entity not exist: {entityId}");
            }
        }
    }
}