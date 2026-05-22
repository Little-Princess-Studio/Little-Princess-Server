// -----------------------------------------------------------------------
// <copyright file="Server.HostConnection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Entity;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
/// redirect to target server instance.
/// </summary>
public partial class Server
{
    private readonly EnumDispatcher<HostCommandType, HostCommand> hostCommandDispatcher
        = new("server.hostCommand", warnOnMissing: false);

    private void InitHostManagerConnection(bool useMqToHostMgr, string hostManagerIp, int hostManagerPort)
    {
        this.hostCommandDispatcher.ScanAndRegister<HostCommandHandlerAttribute>(this);

        if (!useMqToHostMgr)
        {
            this.hostMgrConnection = new ImmediateHostManagerConnectionOfServer(
                hostManagerIp,
                hostManagerPort,
                this.GenerateRpcId,
                () => this.tcpServer!.Stopped,
                () => this.entity?.MailBox);
        }
        else
        {
            this.hostMgrConnection = new MessageQueueHostManagerConnectionOfServer(this.Name, this.GenerateRpcId);
        }

        this.hostMgrConnection.RegisterMessageHandler(
            PackageType.RequireCreateEntityRes,
            this.HandleRequireCreateEntityResFromHost);
        this.hostMgrConnection.RegisterMessageHandler(
            PackageType.CreateDistributeEntity,
            this.HandleCreateDistributeEntityFromHost);
        this.hostMgrConnection.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
        this.hostMgrConnection.RegisterMessageHandler(PackageType.Ping, this.HandlePing);
    }

    private void HandlePing(IMessage message)
    {
        var pong = new Pong
        {
            SenderMailBox = RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox),
        };

        this.hostMgrConnection.Send(pong);
    }

    private void HandleCreateDistributeEntityFromHost(IMessage msg)
    {
        var createDist = (msg as CreateDistributeEntity)!;

        var newId = createDist.EntityId!;
        var entityClassName = createDist.EntityClassName!;
        var jsonDesc = createDist.Description!;

        var entityMailBox = new Common.Rpc.MailBox(newId, this.Ip, this.Port, this.HostNum);
        Task? task = null;

        if (createDist.EntityType == EntityType.ServerClientEntity)
        {
            var connToGate =
                this.GateConnections.FirstOrDefault(conn => conn!.MailBox.Id == createDist.GateId, null);
            if (connToGate != null)
            {
                Logger.Debug("[HandleCreateDistributeEntity] Bind gate conn to new entity");
                task = this.OnCreateEntity(connToGate, entityClassName, jsonDesc, entityMailBox);
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
            task = this.OnCreateEntity(null!, entityClassName, jsonDesc, entityMailBox);
        }

        task?.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Logger.Error(t.Exception);
                    throw t.Exception;
                }

                var createEntityRes = new CreateDistributeEntityRes
                {
                    Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entityMailBox),
                    ConnectionID = createDist.ConnectionID,
                    EntityType = createDist.EntityType,
                    EntityClassName = createDist.EntityClassName,
                };

                Logger.Debug("Create Entity Anywhere");
                this.hostMgrConnection.Send(createEntityRes);
            });
    }

    private void HandleRequireCreateEntityResFromHost(IMessage msg)
    {
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
            case EntityType.DistibuteEntity:
            case EntityType.ServerClientEntity:
                this.CreateDistributeEntity(createRes);
                break;
            case EntityType.GateEntity:
            default:
                Logger.Warn($"Invalid Type: {createRes.EntityType}");
                break;
        }
    }

    private void HandleHostCommand(IMessage msg)
    {
        var hostCmd = (HostCommand)msg;
        Logger.Debug($"Sync gates or service manager from host manager. {hostCmd.Type} {hostCmd.Args.Count}");
        this.hostCommandDispatcher.Dispatch(hostCmd.Type, hostCmd);
    }

    [HostCommandHandler(HostCommandType.SyncGates)]
    private void OnSyncGates(HostCommand cmd)
    {
        this.gatesMailBoxesRegisteredEvent = new CountdownEvent(cmd.Args.Count);
        this.waitForSyncGatesEvent.Signal(1);
    }

    [HostCommandHandler(HostCommandType.SyncServiceManager)]
    private void OnSyncServiceManager(HostCommand cmd)
    {
        this.serviceManagerMailBox = RpcHelper.PbMailBoxToRpcMailBox(RpcHelper.GetMailBox(cmd.Args[0]));
        this.waitForSyncServiceManagerEvent.Signal(1);
    }

    [HostCommandHandler(HostCommandType.Stop)]
    private void OnStop(HostCommand cmd)
    {
        _ = cmd;
        this.Stop();
    }

    [HostCommandHandler(HostCommandType.ShutdownInstance)]
    private void OnShutdownInstance(HostCommand cmd)
    {
        LPS.Server.MessageQueue.ProcessExitCoordinator.Schedule(
            $"server:{this.Name}",
            this.DrainForShutdown,
            cmd.ShutdownTimeoutMs);
    }

    /// <summary>
    /// Per-instance teardown invoked by ProcessExitCoordinator.
    /// The existing <see cref="Stop"/> already covers hostMgrConnection,
    /// webMgrDispatcher and tcpServer; we additionally close the
    /// serviceMgrConnection here because <see cref="Stop"/> does not touch
    /// it (Loop()'s natural exit path does, but we are short-circuiting Loop).
    /// </summary>
    private void DrainForShutdown()
    {
        Logger.Info($"[server:{this.Name}] DrainForShutdown begin.");

        try
        {
            this.Stop();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[server:{this.Name}] Stop() threw during drain: {ex.Message}");
        }

        try
        {
            this.serviceMgrConnection?.ShutDown();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[server:{this.Name}] serviceMgrConnection ShutDown threw: {ex.Message}");
        }

        Logger.Info($"[server:{this.Name}] DrainForShutdown complete.");
    }

    private async Task OnCreateEntity(Connection? gateConn, string entityClassName, string jsonDesc, Common.Rpc.MailBox mailBox)
    {
        Logger.Info($"[OnCreateEntity] Server create a new entity with mailbox {mailBox}");

        var entity = await RpcServerHelper.CreateEntityLocally(entityClassName, jsonDesc);

        entity.SendSyncMessageHandler = (keepOrder, delayTime, syncMsg) =>
        {
            Logger.Info($"Send sync msg {syncMsg.Operation} {syncMsg.MailBox} {syncMsg.RpcPropertyPath}"
                        + $"{syncMsg.RpcSyncPropertyType}:{delayTime}:{keepOrder}");
            this.AddMessageToTimeCircle(syncMsg, delayTime, keepOrder);
        };

        if (entity is ServerClientEntity serverClientEntity)
        {
            // bind gate conn to client entity
            serverClientEntity.BindGateConn((gateConn as SocketConnection)!);
        }
        else if (gateConn != null)
        {
            Logger.Warn(
                $"[OnCreateEntity] Non-ServerClientEntity of {entityClassName} was created with gate connection!");
        }

        entity.OnSendEntityRpc = entityRpc => this.SendEntityRpc(entity, entityRpc);
        entity.OnSendServiceRpc = this.SendServiceRpc;
        entity.OnSendEntityRpcCallback = callback => this.SendEntityRpcCallBack(entity, callback);
        entity.MailBox = mailBox;

        Logger.Debug($"[OnCreateEntity] record local entity: {mailBox.Id}");
        this.localEntityDict[mailBox.Id] = entity!;

        this.defaultCell!.ManuallyAdd(entity);
    }

    private void CreateDistributeEntity(RequireCreateEntityRes requireCreateEntityRes)
    {
        var connId = requireCreateEntityRes.ConnectionID;
        if (this.asyncTaskGeneratorForMailBox.ContainsAsyncId(connId))
        {
            this.asyncTaskGeneratorForMailBox.ResolveAsyncTask(
                connId,
                RpcHelper.PbMailBoxToRpcMailBox(requireCreateEntityRes.Mailbox));
        }
        else
        {
            Logger.Warn($"Invalid CreateDistributeEntity, connId {connId}");
        }
    }

    private void CreateServerDefaultCellEntity(RequireCreateEntityRes createRes)
    {
        var newId = createRes.Mailbox.ID;
        this.defaultCell = new ServerDefaultCellEntity()
        {
            MailBox = new Common.Rpc.MailBox(newId, this.Ip, this.Port, this.HostNum),
            OnSendEntityRpc = entityRpc => this.SendEntityRpc(this.defaultCell!, entityRpc),
            OnSendServiceRpc = this.SendServiceRpc,
            OnSendEntityRpcCallback = callback => this.SendEntityRpcCallBack(this.defaultCell!, callback),
            EntityLeaveCallBack = entity => this.localEntityDict.Remove(entity.MailBox.Id),
            EntityEnterCallBack = (entity, gateMailBox) =>
            {
                entity.OnSendEntityRpc = entityRpc => this.SendEntityRpc(entity, entityRpc);
                entity.OnSendServiceRpc = this.SendServiceRpc;
                entity.OnSendEntityRpcCallback = callback => this.SendEntityRpcCallBack(entity, callback);
                if (entity is ServerClientEntity serverClientEntity)
                {
                    Logger.Debug("transferred new serverClientEntity, bind new conn");
                    var gateConn = this.GateConnections.First(conn => conn.MailBox.CompareOnlyID(gateMailBox));
                    serverClientEntity.BindGateConn(gateConn);
                }

                this.localEntityDict.Add(entity.MailBox.Id, entity);
            },
        };

        this.cells.Add(newId, this.defaultCell);

        this.localEntityGeneratedEvent.Signal(1);
        Logger.Info($"default cell generated, {this.defaultCell.MailBox}.");
    }

    private void CreateServerEntity(RequireCreateEntityRes createRes)
    {
        var serverEntityMailBox =
            new Common.Rpc.MailBox(createRes.Mailbox.ID, this.Ip, this.Port, this.HostNum);
        this.entity = new ServerEntity(serverEntityMailBox)
        {
            // todo: insert local rpc call operation to pump queue, instead of directly calling local entity rpc here.
            OnSendEntityRpc = entityRpc => this.SendEntityRpc(this.entity!, entityRpc),
            OnSendServiceRpc = this.SendServiceRpc,
            OnSendEntityRpcCallback = callback => this.SendEntityRpcCallBack(this.entity!, callback),
        };

        Logger.Info("server entity generated.");

        this.localEntityGeneratedEvent.Signal(1);
    }
}