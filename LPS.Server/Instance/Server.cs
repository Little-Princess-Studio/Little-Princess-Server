// -----------------------------------------------------------------------
// <copyright file="Server.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using LPS.Server.Entity;
using LPS.Server.Instance.HostConnection;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json.Linq;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
/// redirect to target server instance.
/// </summary>
public partial class Server : IInstance
{
    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    /// <inheritdoc/>
    public JToken Config { get; }

    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.Server;

    private readonly Dictionary<string, DistributeEntity> localEntityDict = new();
    private readonly Dictionary<string, CellEntity> cells = new();

    private readonly ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)> timeCircleQueue = new();

    private readonly AsyncTaskGenerator<MailBox> asyncTaskGeneratorForMailBox;

    // todo: use constant value to init time circle
    private readonly TimeCircle timeCircle = new(50, 1000);
    private readonly Random random = new();

    private readonly TcpServer tcpServer;

    private readonly CountdownEvent localEntityGeneratedEvent;
    private readonly CountdownEvent waitForSyncGatesEvent;
    private readonly CountdownEvent waitForSyncServiceManagerEvent;

    private readonly bool isRestart;

    private IManagerConnection hostMgrConnection = null!;
    private IManagerConnection? serviceMgrConnection;

    private MessageQueueClient? messageQueueClientToWebMgr;

    private ServerEntity? entity;
    private CellEntity? defaultCell;

    private Common.Rpc.MailBox serviceManagerMailBox;

    private Connection[] GateConnections => this.tcpServer.AllConnections;

    private uint rpcIdCounter;

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
    /// <param name="useMqToHostMgr">If use message queue to build connection with host manager.</param>
    /// <param name="config">Config of the instance.</param>
    /// <param name="isRestart">If this instance restarting.</param>
    public Server(
        string name,
        string ip,
        int port,
        int hostnum,
        string hostManagerIp,
        int hostManagerPort,
        bool useMqToHostMgr,
        JToken config,
        bool isRestart)
    {
        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostnum;
        this.Config = config;

        this.InitHostManagerConnection(useMqToHostMgr, hostManagerIp, hostManagerPort);

        this.asyncTaskGeneratorForMailBox = new AsyncTaskGenerator<MailBox>
        {
            OnGenerateAsyncId = this.GenerateRpcId,
        };

        this.tcpServer = new TcpServer(ip, port)
        {
            OnInit = this.RegisterServerMessageHandlers,
            OnDispose = this.UnregisterServerMessageHandlers,
            ServerTickHandler = this.OnTick,
        };

        this.timeCircle.Start();

        this.localEntityGeneratedEvent = new CountdownEvent(2);
        this.waitForSyncGatesEvent = new CountdownEvent(1);
        this.waitForSyncServiceManagerEvent = new CountdownEvent(1);
        this.isRestart = isRestart;
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.hostMgrConnection.ShutDown();
        this.messageQueueClientToWebMgr?.ShutDown();
        this.tcpServer.Stop();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info($"Start server at {this.Ip}:{this.Port}");
        this.hostMgrConnection.Run();

        Logger.Info("Host manager connected.");
        Logger.Info("Start time circle pump.");
        var sendQueueSandBox = SandBox.Create(this.TimeCircleSyncMessageEnqueueHandler);
        sendQueueSandBox.Run();

        this.localEntityGeneratedEvent.Wait();
        Logger.Info($"Local entity generated. {this.entity!.MailBox}");

        if (this.isRestart)
        {
            this.HandleRestart();
        }
        else
        {
            // register server and wait for sync ack
            var regCtl = new Control
            {
                From = RemoteType.Server,
                Message = ControlMessage.Ready,
            };
            regCtl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
            this.hostMgrConnection.Send(regCtl);

            Logger.Info("wait for sync gates mailboxes");
            this.waitForSyncGatesEvent.Wait();

            Logger.Info("Start server tcp server.");
            this.tcpServer.Run();

            Logger.Info("wait for gate mailbox registered");
            this.gatesMailBoxesRegisteredEvent!.Wait();

            Logger.Info("wait for service manager registered");
            this.waitForSyncServiceManagerEvent.Wait();
        }

        Logger.Info("Try to connect to service manager");
        this.ConnectToServiceManager();

        Logger.Info("Try to connect to web manager");
        this.InitWebManagerMessageQueueClient();

        // gate main thread will stuck here
        this.hostMgrConnection.WaitForExit();
        this.tcpServer.WaitForExit();
        this.serviceMgrConnection?.WaitForExit();

        this.messageQueueClientToWebMgr!.ShutDown();
    }

    /// <summary>
    /// Notify gate update ServerClientEntity registration.
    /// </summary>
    /// <param name="entity">ServerClientEntity.</param>
    /// <param name="oldMb">Old MailBox.</param>
    /// <param name="newMb">NewMailBox.</param>
    /// <returns>Result.</returns>
    public async Task<bool> NotifyGateUpdateServerClientEntityRegistration(
        ServerClientEntity entity,
        MailBox oldMb,
        MailBox newMb)
    {
        Logger.Debug("Notify gate update registration.");
        var gateMailBox = entity.Client.GateConn.MailBox;
        await this.entity!.Call(gateMailBox, nameof(Gate.UpdateServerClientEntityRegistration), oldMb, newMb);
        return true;
    }

    /// <summary>
    /// Require hostmanager to create entity anywhere.
    /// </summary>
    /// <param name="entityClassName">Entity class name.</param>
    /// <param name="description">Entity class description.</param>
    /// <param name="gateId">GateId of ServerClientEntity.</param>
    /// <returns>MailBox of created entity.</returns>
    public Task<MailBox> CreateEntityAnywhere(string entityClassName, string description, string gateId)
    {
        var (task, connectionId) = this.asyncTaskGeneratorForMailBox.GenerateAsyncTask();

        this.hostMgrConnection.Send(new RequireCreateEntity
        {
            EntityType = gateId != string.Empty ? EntityType.ServerClientEntity : EntityType.DistibuteEntity,
            CreateType = CreateType.Anywhere,
            EntityClassName = entityClassName,
            Description = description,
            ConnectionID = connectionId,
            GateId = gateId,
        });

        return task;
    }

    private void HandleRestart()
    {
        // register server and wait for sync ack
        var regCtl = new Control
        {
            From = RemoteType.Server,
            Message = ControlMessage.Restart,
        };
        regCtl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
        this.hostMgrConnection.Send(regCtl);

        Logger.Info("wait for sync gates mailboxes at restarting");
        this.waitForSyncGatesEvent.Wait();

        Logger.Info("Start server tcp server.");
        this.tcpServer.Run();

        // wait for reconnecting from gate
        Logger.Info("Send wait for reconnect to host manager");
        regCtl = new Control
        {
            From = RemoteType.Server,
            Message = ControlMessage.WaitForReconnect,
        };
        regCtl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
        this.hostMgrConnection.Send(regCtl);

        Logger.Info("wait for gate mailbox registered");
        this.gatesMailBoxesRegisteredEvent!.Wait();

        Logger.Info("wait for service manager registered");
        this.waitForSyncServiceManagerEvent.Wait();
    }

    private uint GenerateRpcId() => this.rpcIdCounter++;

    private void OnTick(uint deltaTime) => this.timeCircle.Tick(deltaTime, this.DispatchSyncCommand);

    private void DispatchSyncCommand(PropertySyncCommandList command)
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
                // Migrate notification
                if (entityRpc.RpcType == RpcType.ServerToClient && baseEntity is ServerClientEntity)
                {
                    var gateConn = (baseEntity as ServerClientEntity)!.Client.GateConn;
                    Logger.Info($"serverToClient rpc send to gate {gateConn.MailBox}");
                    this.tcpServer.Send(entityRpc, gateConn);
                }
                else
                {
                    RpcHelper.CallLocalEntity(entity, entityRpc);
                }
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

    private void SendEntityRpcCallBack(BaseEntity baseEntity, EntityRpcCallBack callback)
    {
        // send this rpc to gate
        var targetMailBox = callback.TargetMailBox;

        // send to self
        if (baseEntity.MailBox.CompareOnlyID(targetMailBox))
        {
            Logger.Info($"rpctype: {callback.RpcType}");
            var rpcType = callback.RpcType;
            if (rpcType == RpcType.ClientToServer || rpcType == RpcType.ServerInside)
            {
                baseEntity.OnRpcCallBack(callback);
            }
            else if (rpcType == RpcType.ServerToClient)
            {
                var gateConn = (baseEntity as ServerClientEntity)!.Client.GateConn;

                Logger.Info($"serverToClient rpc send to gate {gateConn.MailBox}");

                this.tcpServer.Send(callback, gateConn);
            }
            else
            {
                throw new Exception($"Invalid rpc type: {callback.RpcType}");
            }
        }

        // send to local entity
        else if (this.localEntityDict.ContainsKey(targetMailBox.ID))
        {
            var entity = this.localEntityDict[targetMailBox.ID];

            try
            {
                // Migrate notification
                if (callback.RpcType == RpcType.ServerToClient && baseEntity is ServerClientEntity)
                {
                    var gateConn = (baseEntity as ServerClientEntity)!.Client.GateConn;
                    Logger.Info($"serverToClient rpc send to gate {gateConn.MailBox}");
                    this.tcpServer.Send(callback, gateConn);
                }
                else
                {
                    entity.OnRpcCallBack(callback);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception happened when call local entity");
            }
        }
        else
        {
            Logger.Debug("[Server] SendEntityRpcCallBack redirect to gate");

            // redirect to gate
            this.tcpServer.Send(callback, this.GateConnections[0]);
        }
    }

    private void SendServiceRpc(ServiceRpc rpc) => this.serviceMgrConnection?.Send(rpc);
}