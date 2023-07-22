// -----------------------------------------------------------------------
// <copyright file="HostManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

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

namespace LPS.Server.Instance;

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Database;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json.Linq;

/// <summary>
/// Status of the hostmanager.
/// </summary>
#pragma warning disable SA1602
public enum HostStatus
{
    None, // init
    State0,
    State1,
    State2,
    State3, // stopping
    State4, // stopped
}
#pragma warning restore SA1602

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
    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.HostManager;

    /// <summary>
    /// Name of the hostmanager.
    /// </summary>
    public readonly string Name;

    /// <inheritdoc/>
    public int HostNum { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <summary>
    /// Server num of the host.
    /// </summary>
    public readonly int ServerNum;

    /// <summary>
    /// Gate num of the host.
    /// </summary>
    public readonly int GateNum;

    /// <summary>
    /// Gets status of the hostmanager.
    /// </summary>
    public HostStatus Status { get; } = HostStatus.None;

    private readonly List<Common.Rpc.MailBox> serversMailBoxes = new();
    private readonly List<Common.Rpc.MailBox> gatesMailBoxes = new();
    private readonly TcpServer tcpServer;
    private readonly Random random = new Random();

    private readonly Dictionary<uint, (uint ConnId, Action<byte[]>)>
        createDistEntityAsyncRecord = new Dictionary<uint, (uint ConnId, Action<byte[]> Send)>();

    private readonly MessageQueueClient messageQueueClientToWebMgr;
    private readonly MessageQueueClient messageQueueClientToServer;

    private readonly Dictionary<string, Connection> mailboxIdToConnection = new Dictionary<string, Connection>();
    private readonly Dictionary<string, string> mailboxIdToIdentifier = new Dictionary<string, string>();

    private uint createEntityCnt;

    private Dispatcher<(IMessage Mesage, string TargetIdentifier, InstanceType OriType)> dispatcher =
        new Dispatcher<(IMessage Mesage, string TargetIdentifier, InstanceType OriType)>();

    /// <summary>
    /// Initializes a new instance of the <see cref="HostManager"/> class.
    /// </summary>
    /// <param name="name">Name of the hostmanager.</param>
    /// <param name="hostNum">Host num of the hostmanager.</param>
    /// <param name="ip">Ip of the hostmanager.</param>
    /// <param name="port">Port of the hostmanager.</param>
    /// <param name="serverNum">Server number of hostmanager.</param>
    /// <param name="gateNum">Gate number of the hostmanager.</param>
    public HostManager(string name, int hostNum, string ip, int port, int serverNum, int gateNum)
    {
        this.Name = name;
        this.HostNum = hostNum;
        this.Ip = ip;
        this.Port = port;
        this.ServerNum = serverNum;
        this.GateNum = gateNum;

        this.tcpServer = new TcpServer(ip, port)
        {
            OnInit = this.RegisterServerMessageHandlers,
            OnDispose = this.UnregisterServerMessageHandlers,
            ServerTickHandler = null,
        };

        this.dispatcher.Register(
            PackageType.CreateDistributeEntityRes,
            (arg)
                =>
            {
                var (msg, targetIdentifier, instanceType) = arg;
                this.CommonHandleCreateDistributeEntityRes(
                    msg,
                    bytes => this.messageQueueClientToServer!.Publish(
                        bytes,
                        GetExchangeName(instanceType),
                        GenerateRoutingKey(targetIdentifier, instanceType)));
            });

        this.dispatcher.Register(
            PackageType.RequireCreateEntity,
            (arg)
                =>
            {
                var (msg, targetIdentifier, instanceType) = arg;
                this.CommonHandleRequireCreateEntity(
                    msg,
                    bytes => this.messageQueueClientToServer!.Publish(
                        bytes,
                        GetExchangeName(instanceType),
                        GenerateRoutingKey(targetIdentifier, instanceType)));
            });

        this.dispatcher.Register(
            PackageType.Control,
            (arg)
                =>
            {
                var (msg, targetIdentifier, _) = arg;
                this.HandleControlCmdForMqConnection(msg, targetIdentifier);
            });

        this.messageQueueClientToWebMgr = new MessageQueueClient();
        this.messageQueueClientToServer = new MessageQueueClient();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info($"Start Host Manager at {this.Ip}:{this.Port}");
        this.tcpServer.Run();

        this.InitMessageQueueClientInternal();
        this.InitMessageQueueClientToWebManager();

        this.tcpServer.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.tcpServer.Stop();
    }

    private static string GenerateRoutingKey(string identifier, InstanceType instanceType)
    {
        if (instanceType == InstanceType.Gate)
        {
            return Consts.GenerateHostMessageToGatePackage(identifier);
        }

        if (instanceType == InstanceType.Server)
        {
            return Consts.GenerateHostMessageToServerPackage(identifier);
        }

        throw new Exception($"Invalid instanceType : {instanceType}");
    }

    private static string GetExchangeName(InstanceType instanceType)
    {
        string exchangeName = instanceType switch
        {
            InstanceType.Gate => Consts.HostMgrToGateExchangeName,
            InstanceType.Server => Consts.HostMgrToServerExchangeName,
            _ => throw new Exception($"Invalid instance type: {instanceType}"),
        };

        return exchangeName;
    }

    private void InitMessageQueueClientInternal()
    {
        Logger.Debug("Start mq client for server.");
        this.messageQueueClientToServer.Init();
        this.messageQueueClientToServer.AsProducer();
        this.messageQueueClientToServer.AsConsumer();

        this.messageQueueClientToServer.DeclareExchange(Consts.HostMgrToServerExchangeName);
        this.messageQueueClientToServer.DeclareExchange(Consts.HostMgrToGateExchangeName);

        this.messageQueueClientToServer.DeclareExchange(Consts.ServerToHostExchangeName);
        this.messageQueueClientToServer.DeclareExchange(Consts.GateToHostExchangeName);

        // As consumer for server, we should bind exchange of `Consts.ServerToHostExchangeName` with `Consts.ServerMessageQueueName`
        this.messageQueueClientToServer.BindQueueAndExchange(
            Consts.ServerMessageQueueName,
            Consts.ServerToHostExchangeName,
            Consts.RoutingKeyServerToHost);

        // As consumer for gate, we should bind exchange of `Consts.GateToHostExchangeName` with `Consts.GateMessageQueueName`
        this.messageQueueClientToServer.BindQueueAndExchange(
            Consts.GateMessageQueueName,
            Consts.GateToHostExchangeName,
            Consts.RoutingKeyGateToHost);

        this.messageQueueClientToServer.Observe(
            Consts.ServerMessageQueueName,
            (msg, routingKey) =>
            {
                var split = routingKey.Split('.');
                var msgType = split[0];
                var targetIdentifier = split[1];
                Logger.Debug($"Message recieved from server. {msgType} {targetIdentifier} {routingKey}");

                switch (msgType)
                {
                    case "serverMessagePackage":
                        var pkg = MessageBuffer.GetPackageFromBytes(msg);
                        var type = (PackageType)pkg.Header.Type;
                        var protobuf = PackageHelper.GetProtoBufObjectByType(type, pkg);
                        Logger.Debug($"Message package type: {type}");
                        this.dispatcher.Dispatch(type, (protobuf, targetIdentifier, InstanceType.Server));
                        break;
                    default:
                        Logger.Warn($"Unknown message type: {msgType}");
                        break;
                }
            });

        this.messageQueueClientToServer.Observe(
            Consts.GateMessageQueueName,
            (msg, routingKey) =>
            {
                var split = routingKey.Split('.');
                var msgType = split[0];
                var targetIdentifier = split[1];
                switch (msgType)
                {
                    case "serverMessagePackage":
                        var pkg = MessageBuffer.GetPackageFromBytes(msg);
                        var type = (PackageType)pkg.Header.Type;
                        var protobuf = PackageHelper.GetProtoBufObjectByType(type, pkg);
                        this.dispatcher.Dispatch(type, (protobuf, targetIdentifier, InstanceType.Gate));
                        break;
                    default:
                        Logger.Warn($"Unknown message type: {msgType}");
                        break;
                }
            });
    }

    private void InitMessageQueueClientToWebManager()
    {
        Logger.Debug("Start mq client for web manager.");
        this.messageQueueClientToWebMgr.Init();
        this.messageQueueClientToWebMgr.AsProducer();
        this.messageQueueClientToWebMgr.AsConsumer();

        this.messageQueueClientToWebMgr.DeclareExchange(Consts.WebMgrExchangeName);
        this.messageQueueClientToWebMgr.DeclareExchange(Consts.ServerExchangeName);
        this.messageQueueClientToWebMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.Name),
            Consts.WebMgrExchangeName,
            Consts.RoutingKeyToHostManager);

        this.messageQueueClientToWebMgr.Observe(
            Consts.GenerateWebManagerQueueName(this.Name),
            (msg, routingKey) =>
            {
                if (routingKey == Consts.GetServerBasicInfo)
                {
                    var (msgId, _) = MessageQueueJsonBody.From(msg);
                    var res = MessageQueueJsonBody.Create(
                        msgId,
                        new JObject
                        {
                            ["serverCnt"] = this.ServerNum,
                            ["serverMailBoxes"] = new JArray(this.serversMailBoxes.Select(conn => new JObject
                            {
                                ["id"] = conn.Id,
                                ["ip"] = conn.Ip,
                                ["port"] = conn.Port,
                                ["hostNum"] = conn.HostNum,
                            })),
                        });
                    this.messageQueueClientToWebMgr.Publish(
                        res.ToJson(),
                        Consts.ServerExchangeName,
                        Consts.ServerBasicInfoRes);
                }
            });
    }

    private void UnregisterServerMessageHandlers()
    {
        this.tcpServer.UnregisterMessageHandler(PackageType.Control, this.HandleControlCmdForImmediateConnection);
        this.tcpServer.UnregisterMessageHandler(
            PackageType.RequireCreateEntity,
            this.HandleControlCmdForImmediateConnection);
        this.tcpServer.UnregisterMessageHandler(
            PackageType.CreateDistributeEntityRes,
            this.HandleCreateDistributeEntityRes);
    }

    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.Control, this.HandleControlCmdForImmediateConnection);
        this.tcpServer.RegisterMessageHandler(PackageType.RequireCreateEntity, this.HandleRequireCreateEntity);
        this.tcpServer.RegisterMessageHandler(
            PackageType.CreateDistributeEntityRes,
            this.HandleCreateDistributeEntityRes);
    }

    private void HandleCreateDistributeEntityRes((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        this.CommonHandleCreateDistributeEntityRes(msg, (bytes) => { conn.Socket.Send(bytes); });
    }

    private void CommonHandleCreateDistributeEntityRes(IMessage msg, Action<byte[]> send)
    {
        var createRes = (msg as CreateDistributeEntityRes)!;

        Logger.Debug($"HandleCreateDistributeEntityRes, {createRes.Mailbox}");

        if (!this.createDistEntityAsyncRecord.ContainsKey(createRes.ConnectionID))
        {
            Logger.Warn($"Key {createRes.ConnectionID} not in the record.");
            return;
        }

        this.createDistEntityAsyncRecord.Remove(createRes.ConnectionID, out var tp);
        var (oriConnId, sendToOriServer) = tp;

        var requireCreateRes = new RequireCreateEntityRes
        {
            Mailbox = createRes.Mailbox,
            ConnectionID = oriConnId,
            EntityType = createRes.EntityType,
            EntityClassName = createRes.EntityClassName,
        };

        var pkg = PackageHelper.FromProtoBuf(requireCreateRes, 0);
        sendToOriServer(pkg.ToBytes());
    }

    private void HandleRequireCreateEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, id) = arg;
        this.CommonHandleRequireCreateEntity(msg, (bytes) => { conn.Socket.Send(bytes); });
    }

    private void CommonHandleRequireCreateEntity(IMessage msg, Action<byte[]> send)
    {
        var createEntity = (msg as RequireCreateEntity)!;

        Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

        switch (createEntity.CreateType)
        {
            case CreateType.Local:
                this.CreateLocalEntity(createEntity, 0, send);
                break;
            case CreateType.Anywhere:
                this.CreateAnywhereEntity(createEntity, 0, send);
                break;
            case CreateType.Manual:
                this.CreateManualEntity(createEntity, 0, send);
                break;
        }
    }

    private void CreateLocalEntity(RequireCreateEntity createEntity, uint id, Action<byte[]> send) =>
        this.CreateManualEntity(createEntity, id, send);

    private void CreateManualEntity(RequireCreateEntity createEntity, uint id, Action<byte[]> send)
    {
        DbHelper.GenerateNewGlobalId().ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                Logger.Error(task.Exception, "GenerateNewGlobalId failed.");
                return;
            }

            var newId = task.Result;
            var entityMailBox = new RequireCreateEntityRes
            {
                Mailbox = new Common.Rpc.InnerMessages.MailBox
                {
                    IP = string.Empty,
                    Port = 0,
                    HostNum = (uint)this.HostNum,
                    ID = newId,
                },
                EntityType = createEntity.EntityType,
                ConnectionID = createEntity.ConnectionID,
                EntityClassName = createEntity.EntityClassName,
            };
            var pkg = PackageHelper.FromProtoBuf(entityMailBox, id);
            send.Invoke(pkg.ToBytes());
        });
    }

    /// <summary>
    /// server craete eneity anywhere step:
    /// 1. Server (origin server) sends RequireCreateEntity msg to host manager
    /// 2. HostManager randomly selects a server and send CreateEntity msg
    /// 3. Selected server creates entity and sends CreateEntityRes to host manager
    /// 4. Host manager sends CreateEntityRes to origin server.
    /// </summary>
    /// <param name="createEntity">CreateEntity object.</param>
    /// <param name="id">Message id.</param>
    /// <param name="send">Send to create entity.</param>
    private void CreateAnywhereEntity(RequireCreateEntity createEntity, uint id, Action<byte[]> send)
    {
        DbHelper.GenerateNewGlobalId().ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                Logger.Error(task.Exception, "GenerateNewGlobalId failed.");
                return;
            }

            var newId = task.Result;
            Logger.Debug("Randomly select a server");
            var serverMailBox = this.serversMailBoxes[this.random.Next(0, this.serversMailBoxes.Count)];

            Logger.Debug("Create Entity Anywhere");
            var connId = this.createEntityCnt++;
            var createDist = new CreateDistributeEntity
            {
                EntityClassName = createEntity.EntityClassName,
                Description = createEntity.Description,
                ConnectionID = connId,
                EntityId = newId,
                EntityType = createEntity.EntityType,
            };

            if (createEntity.EntityType == EntityType.ServerClientEntity)
            {
                createDist.GateId = createEntity.GateId;
            }

            var pkg = PackageHelper.FromProtoBuf(createDist, id);

            var serverConn = this.mailboxIdToConnection.GetValueOrDefault(serverMailBox.Id);
            if (serverConn != null)
            {
                serverConn.Socket.Send(pkg.ToBytes());
            }
            else
            {
                var targetServer = this.mailboxIdToIdentifier.GetValueOrDefault(serverMailBox.Id);
                if (targetServer != null)
                {
                    this.messageQueueClientToServer.Publish(
                        pkg.ToBytes(),
                        Consts.HostMgrToServerExchangeName,
                        Consts.GenerateHostMessageToServerPackage(targetServer));
                }
                else
                {
                    Logger.Warn($"Server conn {serverMailBox} not found.");
                }
            }

            // record
            this.createDistEntityAsyncRecord[connId] = (createEntity.ConnectionID, send);
        });
    }

    private void HandleControlCmdForImmediateConnection((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        var hostCmd = (msg as Control)!;
        switch (hostCmd.Message)
        {
            case ControlMessage.Ready:
                this.RegisterComponents(
                    hostCmd.From,
                    RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                        .Unpack<Common.Rpc.InnerMessages.MailBox>()),
                    conn);
                break;
            case ControlMessage.Restart:
                break;
            case ControlMessage.ShutDown:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmd.Message), hostCmd.Message, null);
        }
    }

    private void HandleControlCmdForMqConnection(IMessage msg, string targetIdentifier)
    {
        var hostCmd = (msg as Control)!;

        switch (hostCmd.Message)
        {
            case ControlMessage.Ready:
                var mb = RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                    .Unpack<Common.Rpc.InnerMessages.MailBox>());
                this.mailboxIdToIdentifier[mb.Id] = targetIdentifier;
                this.BroadcastSyncMessage(
                    hostCmd.From,
                    RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                        .Unpack<Common.Rpc.InnerMessages.MailBox>()));
                break;
            case ControlMessage.Restart:
                break;
            case ControlMessage.ShutDown:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmd.Message), hostCmd.Message, null);
        }
    }

    private void RegisterComponents(RemoteType hostCmdFrom, Common.Rpc.MailBox mailBox, Connection conn)
    {
        conn.MailBox = mailBox;
        this.mailboxIdToConnection[mailBox.Id] = conn;
        this.BroadcastSyncMessage(hostCmdFrom, mailBox);
    }

    private void BroadcastSyncMessage(RemoteType hostCmdFrom, Common.Rpc.MailBox mailBox)
    {
        switch (hostCmdFrom)
        {
            case RemoteType.Gate:
                Logger.Info($"gate require sync {mailBox}");
                lock (this.gatesMailBoxes)
                {
                    this.gatesMailBoxes.Add(mailBox);
                }

                break;
            case RemoteType.Server:
                Logger.Info($"server require sync {mailBox}");
                lock (this.gatesMailBoxes)
                {
                    this.serversMailBoxes.Add(mailBox);
                }

                break;
            case RemoteType.Dbmanager:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmdFrom), hostCmdFrom, null);
        }

        if (this.serversMailBoxes.Count != this.ServerNum || this.gatesMailBoxes.Count != this.GateNum)
        {
            return;
        }

        Logger.Info("All gates registered, send sync msg");
        Logger.Info("All servers registered, send sync msg");

        var gateConns = this.mailboxIdToConnection.Values.Where(
                conn => this.gatesMailBoxes.FindIndex(mb => mb.CompareOnlyID(conn.MailBox)) != -1)
            .ToList();
        var serverConns = this.mailboxIdToConnection.Values.Where(
                conn => this.serversMailBoxes.FindIndex(mb => mb.CompareOnlyID(conn.MailBox)) != -1)
            .ToList();

        // send gates mailboxes
        var syncCmd = new HostCommand
        {
            Type = HostCommandType.SyncGates,
        };

        foreach (var gateConn in this.gatesMailBoxes)
        {
            syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(gateConn)));
        }

        var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Socket.Send(bytes);
        }

        // to server
        foreach (var serverConn in serverConns)
        {
            serverConn.Socket.Send(bytes);
        }

        if (serverConns.Count != this.ServerNum)
        {
            this.messageQueueClientToServer.Publish(
                bytes,
                Consts.HostMgrToServerExchangeName,
                Consts.HostBroadCastMessagePackageToServer,
                false);
        }

        if (gateConns.Count != this.GateNum)
        {
            this.messageQueueClientToServer.Publish(
                bytes,
                Consts.HostMgrToGateExchangeName,
                Consts.HostBroadCastMessagePackageToGate,
                false);
        }

        // -----------------------------------

        // broadcast sync msg
        syncCmd = new HostCommand
        {
            Type = HostCommandType.SyncServers,
        };

        // send server mailboxes
        foreach (var serverConn in this.serversMailBoxes)
        {
            syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(serverConn)));
        }

        pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
        bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Socket.Send(bytes);
        }

        if (gateConns.Count != this.GateNum)
        {
            this.messageQueueClientToServer.Publish(
                bytes,
                Consts.HostMgrToGateExchangeName,
                Consts.HostBroadCastMessagePackageToGate,
                false);
        }
    }
}