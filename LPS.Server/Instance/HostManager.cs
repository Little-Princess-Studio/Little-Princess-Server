// -----------------------------------------------------------------------
// <copyright file="HostManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

/*
 *         HostMgr
 * |----------|----------|-------------|
 * Server0 Server1 Server2         ServiceMgr
 * |----------|----------|     |-------|--------|
 * Gate0             Gate1   Service0      Service1
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
using System.Threading;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Database;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json.Linq;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Status of the hostmanager.
/// </summary>
#pragma warning disable SA1602
public enum HostStatus
{
    None, // init
    Starting, // startup
    Running, // all instance registered
    Stopping, // stopping
    Stopped, // stopped
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
public partial class HostManager : IInstance
{
    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.HostManager;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public JToken Config { get; }

    /// <summary>
    /// Server num of the server desired to be registered.
    /// </summary>
    public readonly int DesiredServerNum;

    /// <summary>
    /// Gate num of the gates desired to be registered.
    /// </summary>
    public readonly int DesiredGateNum;

    /// <summary>
    /// Gets status of the hostmanager.
    /// </summary>
    public HostStatus Status { get; private set; } = HostStatus.None;

    private readonly List<Common.Rpc.MailBox> serversMailBoxes = new();
    private readonly List<Common.Rpc.MailBox> gatesMailBoxes = new();
    private readonly TcpServer tcpServer;
    private readonly Random random = new();

    private readonly Dictionary<uint, (uint ConnId, Connection)>
        createDistEntityAsyncRecord = new();

    private readonly MessageQueueClient messageQueueClientToWebMgr;
    private readonly MessageQueueClient messageQueueClientToOtherInstances;

    private readonly Dictionary<string, Connection> mailboxIdToConnection = new();
    private readonly Dictionary<string, MqConnection> identifierToMqConnection = new();

    private readonly Dispatcher<(IMessage Message, Connection Connection)> dispatcher =
        new();

    private readonly InstanceStatusManager instanceStatusManager = new();

    private readonly Timer heartBeatTimer;

    private readonly Queue<Action> restartQueue = new();

    private (bool ServicManagerReady, Common.Rpc.MailBox ServiceManagerMailBox) serviceManagerInfo = (false, default);

    private uint createEntityCnt;
    private bool isInstanceRestarting;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostManager"/> class.
    /// </summary>
    /// <param name="name">Name of the hostmanager.</param>
    /// <param name="hostNum">Host num of the hostmanager.</param>
    /// <param name="ip">Ip of the hostmanager.</param>
    /// <param name="port">Port of the hostmanager.</param>
    /// <param name="serverNum">Server number of hostmanager.</param>
    /// <param name="gateNum">Gate number of the hostmanager.</param>
    /// <param name="config">Config of the instance.</param>
    public HostManager(string name, int hostNum, string ip, int port, int serverNum, int gateNum, JToken config)
    {
        this.Name = name;
        this.HostNum = hostNum;
        this.Ip = ip;
        this.Port = port;
        this.DesiredServerNum = serverNum;
        this.DesiredGateNum = gateNum;
        this.Config = config;

        this.tcpServer = new TcpServer(ip, port)
        {
            OnInit = this.RegisterServerMessageHandlers,
            OnDispose = this.UnregisterServerMessageHandlers,
            ServerTickHandler = null,
        };

        this.InitializeMessageDispatcher();

        this.messageQueueClientToWebMgr = new MessageQueueClient();
        this.messageQueueClientToOtherInstances = new MessageQueueClient();

        this.heartBeatTimer = new Timer(_ => this.HeartBeatDetect(), null, Timeout.Infinite, 2000);
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info($"Start Host Manager at {this.Ip}:{this.Port}");
        this.tcpServer.Run();

        this.InitMessageQueueClientToInstances();
        this.InitMessageQueueClientToWebManager();

        this.tcpServer.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.heartBeatTimer.Dispose();
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

        if (instanceType == InstanceType.ServiceManager)
        {
            return Consts.HostMessagePackageToServiceMgrPackage;
        }

        throw new Exception($"Invalid instanceType : {instanceType}");
    }

    private static string GetExchangeName(InstanceType instanceType)
    {
        string exchangeName = instanceType switch
        {
            InstanceType.Gate => Consts.HostMgrToGateExchangeName,
            InstanceType.Server => Consts.HostMgrToServerExchangeName,
            InstanceType.ServiceManager => Consts.HostMgrToServiceMgrExchangeName,
            _ => throw new Exception($"Invalid instance type: {instanceType}"),
        };

        return exchangeName;
    }

    private void InitializeMessageDispatcher()
    {
        this.dispatcher.Register(
            PackageType.CreateDistributeEntityRes,
            (arg)
                =>
            {
                var (msg, conn) = arg;
                this.HandleCreateDistributeEntityRes((msg, conn, ServerGlobal.GenerateRpcId()));
            });

        this.dispatcher.Register(
            PackageType.RequireCreateEntity,
            (arg)
                =>
            {
                var (msg, conn) = arg;
                this.HandleRequireCreateEntity((msg, conn, ServerGlobal.GenerateRpcId()));
            });

        this.dispatcher.Register(
            PackageType.Control,
            (arg)
                =>
            {
                var (msg, conn) = arg;
                this.HandleControlCmdForImmediateConnection((msg, conn, ServerGlobal.GenerateRpcId()));
            });

        this.dispatcher.Register(
            PackageType.Pong,
            (arg)
                =>
            {
                var (msg, conn) = arg;
                this.HandlePongFromImmediateConnection((msg, conn, ServerGlobal.GenerateRpcId()));
            });
    }

    private void InitMessageQueueClientToInstances()
    {
        Logger.Debug("Start mq client for server.");
        this.messageQueueClientToOtherInstances.Init();
        this.messageQueueClientToOtherInstances.AsProducer();
        this.messageQueueClientToOtherInstances.AsConsumer();

        this.messageQueueClientToOtherInstances.DeclareExchange(Consts.HostMgrToServerExchangeName);
        this.messageQueueClientToOtherInstances.DeclareExchange(Consts.HostMgrToGateExchangeName);
        this.messageQueueClientToOtherInstances.DeclareExchange(Consts.HostMgrToServiceMgrExchangeName);

        this.messageQueueClientToOtherInstances.DeclareExchange(Consts.ServerToHostExchangeName);
        this.messageQueueClientToOtherInstances.DeclareExchange(Consts.GateToHostExchangeName);
        this.messageQueueClientToOtherInstances.DeclareExchange(Consts.ServiceMgrToHostExchangeName);

        // As consumer for server, we should bind exchange of `Consts.ServerToHostExchangeName` with `Consts.ServerMessageQueueName`
        this.messageQueueClientToOtherInstances.BindQueueAndExchange(
            Consts.ServerMessageQueueName,
            Consts.ServerToHostExchangeName,
            Consts.RoutingKeyServerToHost);

        // As consumer for gate, we should bind exchange of `Consts.GateToHostExchangeName` with `Consts.GateMessageQueueName`
        this.messageQueueClientToOtherInstances.BindQueueAndExchange(
            Consts.GateMessageQueueName,
            Consts.GateToHostExchangeName,
            Consts.RoutingKeyGateToHost);

        this.messageQueueClientToOtherInstances.BindQueueAndExchange(
            Consts.ServiceManagerMessageQueueName,
            Consts.ServiceMgrToHostExchangeName,
            Consts.RoutingKeyServiceMgrToHost);

        this.messageQueueClientToOtherInstances.Observe(
            Consts.ServerMessageQueueName,
            (msg, routingKey) =>
                this.OnGotMqMessage(
                    msg,
                    routingKey,
                    targetIdentifier => new MqConnection(
                        this.messageQueueClientToOtherInstances,
                        GetExchangeName(InstanceType.Server),
                        GenerateRoutingKey(targetIdentifier, InstanceType.Server))));

        this.messageQueueClientToOtherInstances.Observe(
            Consts.GateMessageQueueName,
            (msg, routingKey) =>
                this.OnGotMqMessage(
                    msg,
                    routingKey,
                    targetIdentifier => new MqConnection(
                        this.messageQueueClientToOtherInstances,
                        GetExchangeName(InstanceType.Gate),
                        GenerateRoutingKey(targetIdentifier, InstanceType.Gate))));

        this.messageQueueClientToOtherInstances.Observe(
            Consts.ServiceManagerMessageQueueName,
            (msg, routingKey) =>
                this.OnGotMqMessage(
                    msg,
                    routingKey,
                    targetIdentifier => new MqConnection(
                        this.messageQueueClientToOtherInstances,
                        GetExchangeName(InstanceType.ServiceManager),
                        GenerateRoutingKey(targetIdentifier, InstanceType.ServiceManager))));
    }

    private void OnGotMqMessage(ReadOnlyMemory<byte> msg, string routingKey, Func<string, MqConnection> onCreateConnection)
    {
        {
            var split = routingKey.Split('.');
            var msgType = split[0];
            var targetIdentifier = split[1];
            switch (msgType)
            {
                case "serverMessagePackage":
                    var pkg = PackageHelper.GetPackageFromBytes(msg);
                    var type = (PackageType)pkg.Header.Type;
                    var protobuf = PackageHelper.GetProtoBufObjectByType(type, pkg);

                    MqConnection conn;
                    if (this.identifierToMqConnection.TryGetValue(targetIdentifier, out var value))
                    {
                        conn = value;
                    }
                    else
                    {
                        conn = onCreateConnection.Invoke(targetIdentifier);
                        this.identifierToMqConnection[targetIdentifier] = conn;
                    }

                    this.dispatcher.Dispatch(type, (protobuf, conn));
                    break;
                default:
                    Logger.Warn($"Unknown message type: {msgType}");
                    break;
            }
        }
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
        this.tcpServer.UnregisterMessageHandler(PackageType.Pong, this.HandlePongFromImmediateConnection);
    }

    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.Control, this.HandleControlCmdForImmediateConnection);
        this.tcpServer.RegisterMessageHandler(PackageType.RequireCreateEntity, this.HandleRequireCreateEntity);
        this.tcpServer.RegisterMessageHandler(
            PackageType.CreateDistributeEntityRes,
            this.HandleCreateDistributeEntityRes);
        this.tcpServer.RegisterMessageHandler(PackageType.Pong, this.HandlePongFromImmediateConnection);
    }

    private void HandleCreateDistributeEntityRes((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        var createRes = (msg as CreateDistributeEntityRes)!;

        Logger.Debug($"HandleCreateDistributeEntityRes, {createRes.Mailbox}");

        if (!this.createDistEntityAsyncRecord.ContainsKey(createRes.ConnectionID))
        {
            Logger.Warn($"Key {createRes.ConnectionID} not in the record.");
            return;
        }

        this.createDistEntityAsyncRecord.Remove(createRes.ConnectionID, out var tp);
        var (oriConnId, connToSend) = tp;

        var requireCreateRes = new RequireCreateEntityRes
        {
            Mailbox = createRes.Mailbox,
            ConnectionID = oriConnId,
            EntityType = createRes.EntityType,
            EntityClassName = createRes.EntityClassName,
        };

        connToSend.Send(requireCreateRes);
    }

    private void HandleRequireCreateEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, id) = arg;
        var createEntity = (msg as RequireCreateEntity)!;

        Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

        switch (createEntity.CreateType)
        {
            case CreateType.Local:
                this.CreateLocalEntity(createEntity, ServerGlobal.GenerateRpcId(), conn);
                break;
            case CreateType.Anywhere:
                this.CreateAnywhereEntity(createEntity, ServerGlobal.GenerateRpcId(), conn);
                break;
            case CreateType.Manual:
                this.CreateManualEntity(createEntity, ServerGlobal.GenerateRpcId(), conn);
                break;
        }
    }

    private void CreateLocalEntity(RequireCreateEntity createEntity, uint id, Connection conn) =>
        this.CreateManualEntity(createEntity, id, conn);

    private void CreateManualEntity(RequireCreateEntity createEntity, uint id, Connection conn)
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

            Logger.Debug($"Send CreateEntityRes to origin instance. {entityMailBox} {id} {createEntity.ConnectionID}");
            conn.Send(pkg.ToBytes());
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
    /// <param name="conn">Connection to create entity.</param>
    private void CreateAnywhereEntity(RequireCreateEntity createEntity, uint id, Connection conn)
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
                serverConn.Send(pkg.ToBytes());
            }
            else
            {
                Logger.Warn($"Server conn {serverMailBox} not found.");
            }

            // record
            this.createDistEntityAsyncRecord[connId] = (createEntity.ConnectionID, conn);
        });
    }

    private void HandleControlCmdForImmediateConnection((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        var hostCmd = (msg as Control)!;

        switch (hostCmd.Message)
        {
            case ControlMessage.Ready:
                this.RegisterInstance(
                    hostCmd.From,
                    RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                        .Unpack<Common.Rpc.InnerMessages.MailBox>()),
                    conn);
                break;
            case ControlMessage.Restart:
                if (!this.isInstanceRestarting)
                {
                    this.isInstanceRestarting = true;
                    this.RestartInstance(
                        hostCmd.From,
                        RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                            .Unpack<Common.Rpc.InnerMessages.MailBox>()),
                        conn);
                    Logger.Info("Restating instance from immediate connection.");
                }
                else
                {
                    // todo: filter duplicate restart instance by mailbox
                    this.restartQueue.Enqueue(() =>
                    {
                        this.HandleControlCmdForImmediateConnection(arg);
                    });
                    Logger.Info("Already restarting instance, enqueue the command handler.");
                }

                break;
            case ControlMessage.ShutDown:
                break;
            case ControlMessage.ReconnectEnd:
                this.HandleReconnectEnd();
                break;
            case ControlMessage.WaitForReconnect:
                Logger.Debug($"Remote {hostCmd.From} is waiting for reconnect.");
                this.NotifyReconnect(
                    hostCmd.From,
                    RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                        .Unpack<Common.Rpc.InnerMessages.MailBox>()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmd.Message), hostCmd.Message, null);
        }
    }

    private void HandleReconnectEnd()
    {
        if (this.isInstanceRestarting)
        {
            if (this.restartQueue.Count == 0)
            {
                this.isInstanceRestarting = false;
            }
            else
            {
                var handler = this.restartQueue.Dequeue();
                handler.Invoke();
            }
        }
        else
        {
            Logger.Warn("ReconnectEnd received but no instance is restarting.");
        }
    }
}