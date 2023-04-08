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
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Database;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages.ProtobufDefs;
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

    private readonly List<Connection> serversConn = new List<Connection>();
    private readonly List<Connection> gatesConn = new List<Connection>();
    private readonly TcpServer tcpServer;
    private readonly Random random = new Random();

    private readonly Dictionary<uint, (uint ConnId, Connection OriginConn)>
        createDistEntityAsyncRecord = new Dictionary<uint, (uint ConnId, Connection OriginConn)>();

    private MessageQueueClient? messageQueueClientToWebMgr;
    private uint createEntityCnt;

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
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info($"Start Host Manager at {this.Ip}:{this.Port}");
        this.tcpServer.Run();

        this.InitMessageQueueClient();

        this.tcpServer.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.tcpServer.Stop();
    }

    private void InitMessageQueueClient()
    {
        Logger.Debug("Start mq client.");
        this.messageQueueClientToWebMgr = new MessageQueueClient();
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
                            ["serverMailBoxes"] = new JArray(this.serversConn.Select(conn => new JObject
                            {
                                ["id"] = conn.MailBox.Id,
                                ["ip"] = conn.MailBox.Ip,
                                ["port"] = conn.MailBox.Port,
                                ["hostNum"] = conn.MailBox.HostNum,
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
        this.tcpServer.UnregisterMessageHandler(PackageType.Control, this.HandleControlCmd);
        this.tcpServer.UnregisterMessageHandler(PackageType.RequireCreateEntity, this.HandleControlCmd);
        this.tcpServer.UnregisterMessageHandler(
            PackageType.CreateDistributeEntityRes,
            this.HandleCreateDistributeEntityRes);
    }

    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.Control, this.HandleControlCmd);
        this.tcpServer.RegisterMessageHandler(PackageType.RequireCreateEntity, this.HandleRequireCreateEntity);
        this.tcpServer.RegisterMessageHandler(
            PackageType.CreateDistributeEntityRes,
            this.HandleCreateDistributeEntityRes);
    }

    private void HandleCreateDistributeEntityRes((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, id) = arg;
        var createRes = (msg as CreateDistributeEntityRes)!;

        Logger.Debug($"HandleCreateDistributeEntityRes, {createRes.Mailbox}");

        if (!this.createDistEntityAsyncRecord.ContainsKey(createRes.ConnectionID))
        {
            Logger.Warn($"Key {createRes.ConnectionID} not in the record.");
            return;
        }

        this.createDistEntityAsyncRecord.Remove(createRes.ConnectionID, out var tp);
        var (oriConnId, conn) = tp;

        var requireCreateRes = new RequireCreateEntityRes
        {
            Mailbox = createRes.Mailbox,
            ConnectionID = oriConnId,
            EntityType = createRes.EntityType,
            EntityClassName = createRes.EntityClassName,
        };

        var pkg = PackageHelper.FromProtoBuf(requireCreateRes, id);
        conn.Socket.Send(pkg.ToBytes());
    }

    private void HandleRequireCreateEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, id) = arg;
        var createEntity = (msg as RequireCreateEntity)!;

        Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

        switch (createEntity.CreateType)
        {
            case CreateType.Local:
                this.CreateLocalEntity(createEntity, id, conn);
                break;
            case CreateType.Anywhere:
                this.CreateAnywhereEntity(createEntity, id, conn);
                break;
            case CreateType.Manual:
                this.CreateManualEntity(createEntity, id, conn);
                break;
        }
    }

    private void CreateLocalEntity(RequireCreateEntity createEntity, uint id, Connection conn) =>
        this.CreateManualEntity(createEntity, id, conn);

    private void CreateManualEntity(RequireCreateEntity createEntity, uint id, Connection conn)
    {
        DbHelper.GenerateNewGlobalId().ContinueWith(task =>
        {
            var newId = task.Result;
            var entityMailBox = new RequireCreateEntityRes
            {
                Mailbox = new Common.Rpc.InnerMessages.ProtobufDefs.MailBox
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
            conn.Socket.Send(pkg.ToBytes());
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
    /// <param name="conn">Connection require to create entity.</param>
    private void CreateAnywhereEntity(RequireCreateEntity createEntity, uint id, Connection conn)
    {
        DbHelper.GenerateNewGlobalId().ContinueWith(task =>
        {
            var newId = task.Result;
            Logger.Debug("Randomly select a server");
            var serverConn = this.serversConn[this.random.Next(0, this.serversConn.Count)];

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
            serverConn.Socket.Send(pkg.ToBytes());

            // record
            this.createDistEntityAsyncRecord[connId] = (createEntity.ConnectionID, conn);
        });
    }

    private void HandleControlCmd((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        var hostCmd = (msg as Control)!;
        switch (hostCmd.Message)
        {
            case ControlMessage.Ready:
                this.RegisterComponents(
                    hostCmd.From,
                    RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0]
                        .Unpack<Common.Rpc.InnerMessages.ProtobufDefs.MailBox>()),
                    conn);
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
            this.gatesConn.Add(conn);
        }
        else if (hostCmdFrom == RemoteType.Server)
        {
            Logger.Info($"server require sync {mailBox}");
            this.serversConn.Add(conn);
        }

        if (this.serversConn.Count == this.ServerNum && this.gatesConn.Count == this.GateNum)
        {
            Logger.Info("All gates registered, send sync msg");
            Logger.Info("All servers registered, send sync msg");

            // send gates mailboxes
            var syncCmd = new HostCommand
            {
                Type = HostCommandType.SyncGates,
            };

            foreach (var gateConn in this.gatesConn)
            {
                syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(gateConn.MailBox)));
            }

            var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);

            // to gates
            foreach (var gateConn in this.gatesConn)
            {
                gateConn.Socket.Send(pkg.ToBytes());
            }

            // to server
            foreach (var serverConn in this.serversConn)
            {
                serverConn.Socket.Send(pkg.ToBytes());
            }

            // -----------------------------------

            // broadcast sync msg
            syncCmd = new HostCommand
            {
                Type = HostCommandType.SyncServers,
            };

            // send server mailboxes
            foreach (var serverConn in this.serversConn)
            {
                syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(serverConn.MailBox)));
            }

            pkg = PackageHelper.FromProtoBuf(syncCmd, 0);

            // to gates
            foreach (var gateConn in this.gatesConn)
            {
                gateConn.Socket.Send(pkg.ToBytes());
            }
        }
    }
}