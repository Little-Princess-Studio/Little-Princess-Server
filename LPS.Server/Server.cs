// -----------------------------------------------------------------------
// <copyright file="Server.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.InnerMessages.ProtobufDefs;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages.ProtobufDefs;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
/// redirect to target server instance.
/// </summary>
public class Server : IInstance
{
    /// <summary>
    /// Gets the name of the server.
    /// </summary>
    public string Name { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.Server;

    private readonly Dictionary<string, DistributeEntity> localEntityDict = new();
    private readonly Dictionary<string, CellEntity> cells = new();

    private readonly ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)> timeCircleQueue = new();

    // todo: use constant value to init time circle
    private readonly TimeCircle timeCircle = new(50, 1000);
    private readonly Random random = new();

    private readonly TcpServer tcpServer;
    private readonly TcpClient clientToHostManager;

    private readonly CountdownEvent localEntityGeneratedEvent;
    private readonly CountdownEvent waitForSyncGatesEvent;

    private readonly CountdownEvent hostManagerConnectedEvent;

    private readonly SandBox clientsPumpMsgSandBox;

    private ServerEntity? entity;
    private CellEntity? defaultCell;

    private Connection[] GateConnections => this.tcpServer.AllConnections;

    private uint createEntityCounter;
    private CountdownEvent? gatesMailBoxesRegisteredEvent;

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="name">Name of the server.</param>
    /// <param name="ip">Ip of the server.</param>
    /// <param name="port">Port of the server.</param>
    /// <param name="hostnum">Hostnum of the server.</param>
    /// <param name="hostManagerIp">Ip of the hostmanager.</param>
    /// <param name="hostManagerPort">Port of the hostmanager.</param>
    public Server(string name, string ip, int port, int hostnum, string hostManagerIp, int hostManagerPort)
    {
        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostnum;

        this.tcpServer = new TcpServer(ip, port)
        {
            OnInit = this.RegisterServerMessageHandlers,
            OnDispose = this.UnregisterServerMessageHandlers,
            ServerTickHandler = this.OnTick,
        };

        this.timeCircle.Start();

        this.localEntityGeneratedEvent = new(2);
        this.hostManagerConnectedEvent = new(1);
        this.waitForSyncGatesEvent = new CountdownEvent(1);
        this.clientToHostManager = new TcpClient(
            hostManagerIp,
            hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>())
        {
            OnInit = _ =>
            {
                this.clientToHostManager!.RegisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleRequireCreateEntityResFromHost);
                this.clientToHostManager.RegisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleCreateDistributeEntity);
                this.clientToHostManager.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
            },
            OnDispose = _ =>
            {
                this.clientToHostManager!.UnregisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleRequireCreateEntityResFromHost);
                this.clientToHostManager.UnregisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleCreateDistributeEntity);
                this.clientToHostManager.UnregisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
            },
            OnConnected = _ =>
            {
                this.clientToHostManager!.Send(new RequireCreateEntity
                {
                    EntityType = EntityType.ServerEntity,
                    CreateType = CreateType.Manual,
                    EntityClassName = string.Empty,
                    Description = string.Empty,
                    ConnectionID = this.createEntityCounter++,
                });

                this.clientToHostManager.Send(new RequireCreateEntity
                {
                    EntityType = EntityType.ServerDefaultCellEntity,
                    CreateType = CreateType.Manual,
                    EntityClassName = string.Empty,
                    Description = string.Empty,
                    ConnectionID = this.createEntityCounter++,
                });

                this.hostManagerConnectedEvent.Signal();
            },
        };

        this.clientsPumpMsgSandBox = SandBox.Create(this.PumpMessageHandler);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.clientToHostManager.Stop();
        this.tcpServer.Stop();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info($"Start server at {this.Ip}:{this.Port}");
        this.tcpServer.Run();
        this.clientToHostManager.Run();
        this.hostManagerConnectedEvent.Wait();

        Logger.Debug("Host manager connected.");

        this.clientsPumpMsgSandBox.Run();

        Logger.Debug($"Start time circle pump.");
        var sendQueueSandBox = SandBox.Create(this.TimeCircleSyncMessageEnqueueHandler);
        sendQueueSandBox.Run();

        this.localEntityGeneratedEvent.Wait();
        Logger.Debug($"Local entity generated. {this.entity!.MailBox}");

        // register server and wait for sync ack
        var regCtl = new Control
        {
            From = RemoteType.Server,
            Message = ControlMessage.Ready,
        };
        regCtl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
        this.clientToHostManager.Send(regCtl);

        Logger.Debug("wait for sync gates mailboxes");
        this.waitForSyncGatesEvent.Wait();

        Logger.Debug("wait for gate mailbox registered");
        this.gatesMailBoxesRegisteredEvent!.Wait();

        // gate main thread will stuck here
        this.clientToHostManager.WaitForExit();
        this.tcpServer.WaitForExit();
        this.clientsPumpMsgSandBox.WaitForExit();
    }

    private void OnTick(uint deltaTime)
    {
        this.timeCircle.Tick(deltaTime, command =>
        {
            var entityId = command.EntityId;
            var entity = this.localEntityDict[entityId];
            Connection gateConn;

            // todo: handle sync to local shadow entity
            if (entity is ServerClientEntity serverClientEntity)
            {
                gateConn = serverClientEntity.Client.GateConn;
            }
            else
            {
                gateConn = this.GateConnections[this.random.Next(0, this.GateConnections.Length)];
            }

            this.tcpServer.Send(command, gateConn);
            Logger.Debug($"[Dispatch Prop Sync msg]: {command} to {gateConn.MailBox}");
        });
    }

    private void TimeCircleSyncMessageEnqueueHandler()
    {
        while (!this.tcpServer.Stopped)
        {
            while (!this.timeCircleQueue.IsEmpty)
            {
                var res = this.timeCircleQueue.TryDequeue(out var tp);
                if (res)
                {
                    Logger.Debug("Time circle not empty, pump message");
                    var (keepOrder, delayTime, msg) = tp;
                    this.timeCircle.AddPropertySyncMessage(msg, delayTime, keepOrder);
                }
            }

            Thread.Sleep(1);
        }
    }

    private void AddMessageToTimeCircle(RpcPropertySyncMessage msg, uint delayTimeByMilliseconds, bool keepOrder)
        => this.timeCircleQueue.Enqueue((keepOrder, delayTimeByMilliseconds, msg));

    private void SendEntityRpc(BaseEntity baseEntity, EntityRpc entityRpc)
    {
        // send this rpc to gate
        var targetMailBox = entityRpc.EntityMailBox;

        // send to self
        if (baseEntity.MailBox.CompareOnlyID(targetMailBox))
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

                this.tcpServer.Send(entityRpc, gateConn);
            }
            else
            {
                throw new Exception($"Invalid rpc type: {entityRpc.RpcType}");
            }
        }

        // send to local entity
        else if (this.localEntityDict.ContainsKey(targetMailBox.ID))
        {
            var entity = this.localEntityDict[targetMailBox.ID];

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
            this.tcpServer.Send(entityRpc, this.GateConnections[0]);
        }
    }

    private void OnCreateEntity(Connection? gateConn, string entityClassName, string jsonDesc, MailBox mailBox)
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
            serverClientEntity.BindGateConn(gateConn!);
        }

        entity.OnSend = entityRpc => this.SendEntityRpc(entity, entityRpc);
        entity.MailBox = mailBox;

        this.localEntityDict[mailBox.Id] = entity;

        this.defaultCell!.ManuallyAdd(entity);
    }

    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.tcpServer.RegisterMessageHandler(PackageType.RequirePropertyFullSync, this.HandleRequirePropertyFullSync);
        this.tcpServer.RegisterMessageHandler(PackageType.PropertyFullSyncAck, this.HandlePropertyFullSyncAck);
        this.tcpServer.RegisterMessageHandler(PackageType.Control, this.HandleControl);
    }

    private void UnregisterServerMessageHandlers()
    {
        this.tcpServer.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.tcpServer.UnregisterMessageHandler(
            PackageType.RequirePropertyFullSync,
            this.HandleRequirePropertyFullSync);
        this.tcpServer.UnregisterMessageHandler(PackageType.PropertyFullSyncAck, this.HandlePropertyFullSyncAck);
        this.tcpServer.UnregisterMessageHandler(PackageType.Control, this.HandleControl);
    }

    private void HandleControl(object arg)
    {
        var (msg, connToGate, _) = ((IMessage, Connection, uint))arg;
        var createDist = (msg as Control)!;

        var gateMailBox = createDist.Args[0].Unpack<Common.Rpc.InnerMessages.ProtobufDefs.MailBox>();
        connToGate.MailBox = RpcHelper.PbMailBoxToRpcMailBox(gateMailBox);
        Logger.Info($"Register gates' mailbox {connToGate.MailBox}");

        this.gatesMailBoxesRegisteredEvent!.Signal(1);
    }

    private void HandleCreateDistributeEntity(object arg)
    {
        var (msg, connToHost, id) = ((IMessage, Connection, uint))arg;
        var createDist = (msg as CreateDistributeEntity)!;

        var newId = createDist.EntityId!;
        var entityClassName = createDist.EntityClassName!;
        var jsonDesc = createDist.Description!;

        var entityMailBox = new MailBox(newId, this.Ip, this.Port, this.HostNum);

        if (createDist.EntityType == EntityType.ServerClientEntity)
        {
            var connToGate =
                this.GateConnections.FirstOrDefault(conn => conn!.MailBox.Id == createDist.GateId, null);
            if (connToGate != null)
            {
                Logger.Debug("Bind gate conn to new entity");
                this.OnCreateEntity(connToGate, entityClassName, jsonDesc, entityMailBox);
            }
            else
            {
                // todo: HostManager create task time out
                var ex = new Exception($"conn to gate {createDist.GateId} not exist!");
                Logger.Error(ex);
                throw ex;
            }
        }
        else
        {
            this.OnCreateEntity(null!, entityClassName, jsonDesc, entityMailBox);
        }

        var createEntityRes = new CreateDistributeEntityRes
        {
            Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entityMailBox),
            ConnectionID = createDist.ConnectionID,
            EntityType = createDist.EntityType,
            EntityClassName = createDist.EntityClassName,
        };

        Logger.Debug("Create Entity Anywhere");
        var pkg = PackageHelper.FromProtoBuf(createEntityRes, id);
        connToHost.Socket.Send(pkg.ToBytes());
    }

    private void HandleRequireCreateEntityResFromHost(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var createRes = (msg as RequireCreateEntityRes)!;

        Logger.Info($"Create Entity Res: {createRes.EntityType} {createRes.ConnectionID}");

        switch (createRes.EntityType)
        {
            case EntityType.ServerEntity:
                this.CreateServerEntity(createRes);
                break;
            case EntityType.ServerDefaultCellEntity:
                this.CreateServerDefaultCellEntity(createRes);
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
        this.defaultCell = new ServerDefaultCellEntity()
        {
            MailBox = new MailBox(newId, this.Ip, this.Port, this.HostNum),
            OnSend = entityRpc => this.SendEntityRpc(this.defaultCell!, entityRpc),
            EntityLeaveCallBack = entity => this.localEntityDict.Remove(entity.MailBox.Id),
            EntityEnterCallBack = (entity, gateMailBox) =>
            {
                entity.OnSend = entityRpc => this.SendEntityRpc(entity, entityRpc);
                if (entity is ServerClientEntity serverClientEntity)
                {
                    Logger.Debug("transferred new serverClientEntity, bind new conn");
                    var gateConn = this.GateConnections.First(conn => conn.MailBox.CompareOnlyID(gateMailBox));
                    serverClientEntity.BindGateConn(gateConn);
                }

                this.localEntityDict.Add(entity.MailBox.Id, entity);
            },
        };

        Logger.Info($"default cell generated, {this.defaultCell.MailBox}.");
        this.cells.Add(newId, this.defaultCell);

        this.localEntityGeneratedEvent.Signal(1);
    }

    private void CreateServerEntity(RequireCreateEntityRes createRes)
    {
        var serverEntityMailBox =
            new MailBox(createRes.Mailbox.ID, this.Ip, this.Port, this.HostNum);
        this.entity = new ServerEntity(serverEntityMailBox)
        {
            // todo: insert local rpc call operation to pump queue, instead of directly calling local entity rpc here.
            OnSend = entityRpc => this.SendEntityRpc(this.entity!, entityRpc),
        };

        Logger.Info("server entity generated.");

        this.localEntityGeneratedEvent.Signal(1);
    }

    private void HandleHostCommand(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var hostCmd = (msg as HostCommand)!;

        if (hostCmd.Type == HostCommandType.SyncGates)
        {
            this.gatesMailBoxesRegisteredEvent = new CountdownEvent(hostCmd.Args.Count);
            this.waitForSyncGatesEvent.Signal(1);
        }
    }

    // how server handle entity rpc
    private void HandleEntityRpc(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var entityRpc = (msg as EntityRpc)!;

        var targetMailBox = entityRpc.EntityMailBox;

        if (this.entity!.MailBox.CompareOnlyID(targetMailBox))
        {
            Logger.Debug($"Call server entity: {entityRpc.MethodName}");
            try
            {
                RpcHelper.CallLocalEntity(this.entity, entityRpc);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception happend when call server entity");
            }
        }
        else if (this.cells.ContainsKey(targetMailBox.ID))
        {
            var cell = this.cells[targetMailBox.ID];
            try
            {
                RpcHelper.CallLocalEntity(cell, entityRpc);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception happened when call cell");
            }
        }
        else if (this.localEntityDict.ContainsKey(targetMailBox.ID))
        {
            var entity = this.localEntityDict[targetMailBox.ID];
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
            this.tcpServer.Send(entityRpc, this.GateConnections[0]);
        }
    }

    private void PumpMessageHandler()
    {
        try
        {
            while (!this.tcpServer.Stopped)
            {
                this.clientToHostManager.Pump();
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
        var (msg, conn, id) = ((IMessage, Connection, uint))arg;
        var requirePropertyFullSyncMsg = (msg as RequirePropertyFullSync)!;
        var entityId = requirePropertyFullSyncMsg.EntityId;

        if (this.localEntityDict.ContainsKey(entityId))
        {
            Logger.Debug("Prepare for full sync");
            var entity = this.localEntityDict[entityId];
            entity.FullSync((_, content) =>
            {
                Logger.Debug("Full sync send back");

                var fullSync = new PropertyFullSync
                {
                    EntityId = entityId,
                    PropertyTree = content,
                };
                var pkg = PackageHelper.FromProtoBuf(fullSync, id);
                conn.Socket.Send(pkg.ToBytes());
            });
        }
        else
        {
            throw new Exception($"Entity not exist: {entityId}");
        }
    }

    private void HandlePropertyFullSyncAck(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var propertyFullSyncAckMsg = (msg as PropertyFullSyncAck)!;
        var entityId = propertyFullSyncAckMsg.EntityId;

        if (this.localEntityDict.ContainsKey(entityId))
        {
            var entity = this.localEntityDict[entityId];
            entity.FullSyncAck();
            Logger.Info("Full sync ack succ");
        }
        else
        {
            throw new Exception($"Entity not exist: {entityId}");
        }
    }
}