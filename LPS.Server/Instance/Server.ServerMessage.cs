// -----------------------------------------------------------------------
// <copyright file="Server.ServerMessage.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Entity;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
/// redirect to target server instance.
/// </summary>
public partial class Server
{
    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.tcpServer.RegisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleEntityRpcCallBack);
        this.tcpServer.RegisterMessageHandler(PackageType.RequirePropertyFullSync, this.HandleRequirePropertyFullSync);
        this.tcpServer.RegisterMessageHandler(PackageType.RequireComponentSync, this.HandleRequireComponentSync);
        this.tcpServer.RegisterMessageHandler(PackageType.Control, this.HandleControl);
    }

    private void UnregisterServerMessageHandlers()
    {
        this.tcpServer.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.tcpServer.UnregisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleEntityRpcCallBack);
        this.tcpServer.UnregisterMessageHandler(
            PackageType.RequirePropertyFullSync,
            this.HandleRequirePropertyFullSync);
        this.tcpServer.UnregisterMessageHandler(
            PackageType.RequireComponentSync,
            this.HandleRequireComponentSync);
        this.tcpServer.UnregisterMessageHandler(PackageType.Control, this.HandleControl);
    }

    // how server handle entity rpc
    private void HandleEntityRpc((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
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

    private void HandleEntityRpcCallBack((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var callback = (msg as EntityRpcCallBack)!;

        var targetMailBox = callback.TargetMailBox;

        if (this.entity!.MailBox.CompareOnlyID(targetMailBox))
        {
            Logger.Debug($"Rpc Callback");
            try
            {
                this.entity!.OnRpcCallBack(callback);
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
                cell.OnRpcCallBack(callback);
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
                entity.OnRpcCallBack(callback);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception happened when call server distribute entity");
            }
        }
        else
        {
            // redirect to gate
            this.tcpServer.Send(callback, this.GateConnections[0]);
        }
    }

    private void HandleRequirePropertyFullSync((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Debug("[Server] HandleRequirePropertyFullSync");
        var (msg, conn, id) = arg;
        var requirePropertyFullSyncMsg = (msg as RequirePropertyFullSync)!;
        var entityId = requirePropertyFullSyncMsg.EntityId;

        if (this.localEntityDict.ContainsKey(entityId))
        {
            Logger.Debug("Prepare for full sync");
            DistributeEntity? entity = this.localEntityDict[entityId];
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

    private void HandleRequireComponentSync((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Debug("[Server] HandleRequireComponentSync");
        var (msg, conn, id) = arg;
        var requireComponentSyncMsg = (msg as RequireComponentSync)!;
        var entityId = requireComponentSyncMsg.EntityId;
        var componentName = requireComponentSyncMsg.ComponentName;

        if (this.localEntityDict.ContainsKey(entityId))
        {
            Logger.Debug("Prepare for component sync");
            DistributeEntity? entity = this.localEntityDict[entityId];
            entity.ComponentSync(componentName, (_, content) =>
            {
                Logger.Debug("Component sync send back");

                var compSync = new ComponentSync
                {
                    EntityId = entityId,
                    ComponentName = componentName,
                    PropertyTree = content,
                };
                var pkg = PackageHelper.FromProtoBuf(compSync, id);
                conn.Socket.Send(pkg.ToBytes());
            });
        }
        else
        {
            throw new Exception($"Entity not exist: {entityId}");
        }
    }

    // todo: get all gates' mailboxes -> start tcp server -> waiting for connection from gates
    private void HandleControl((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, connToGate, _) = arg;
        var createDist = (msg as Control)!;

        var gateMailBox = createDist.Args[0].Unpack<Common.Rpc.InnerMessages.MailBox>();
        connToGate.MailBox = RpcHelper.PbMailBoxToRpcMailBox(gateMailBox);
        Logger.Info($"Register gates' mailbox {connToGate.MailBox}");

        this.gatesMailBoxesRegisteredEvent!.Signal(1);
    }
}