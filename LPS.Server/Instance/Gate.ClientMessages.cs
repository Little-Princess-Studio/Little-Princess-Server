// -----------------------------------------------------------------------
// <copyright file="Gate.ClientMessages.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;

/// <summary>
/// Each gate need maintain multiple connections from remote clients
/// and maintain a connection to hostmanager.
/// For hostmanager, gate is a client
/// for remote clients, gate is a server.
/// All the gate mailbox info will be saved in redis, and gate will
/// repeatly sync these info from redis.
/// </summary>
public partial class Gate
{
    private void RegisterGateMessageHandlers(int serverIdx)
    {
        var client = this.tcpClientsToServer![serverIdx];

        void EntityRpcHandler((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleEntityRpcFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.EntityRpc)] = EntityRpcHandler;

        void EntityRpcCallBackHandler((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleEntityRpcCallBackFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.EntityRpcCallBack)] = EntityRpcCallBackHandler;

        void PropertyFullSync((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandlePropertyFullSyncFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.PropertyFullSync)] = PropertyFullSync;

        void PropSyncCommandList((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandlePropertySyncCommandListFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.PropertySyncCommandList)] = PropSyncCommandList;

        void ComponentSync((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleComponentSyncFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.ComponentSync)] = ComponentSync;

        // Shadow-entity inbound messages (per-server TcpClient bus). The Server
        // initiates RequireCreateShadowEntity / RequireDestroyShadowEntity on
        // its inbound socket (Server.tcpServer.Send(req, gateConn)) which
        // round-trips back to THIS Gate's per-server TcpClient bus. Register
        // here, not on tcpGateServer (which is the listening side for Client
        // and other-Gate connections, not for Server replies).
        void RequireCreateShadow((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleRequireCreateShadowEntityFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.RequireCreateShadowEntity)] = RequireCreateShadow;

        void RequireCreateShadowRes((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleRequireCreateShadowEntityResFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.RequireCreateShadowEntityRes)] = RequireCreateShadowRes;

        void RequireDestroyShadow((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleRequireDestroyShadowEntityFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.RequireDestroyShadowEntity)] = RequireDestroyShadow;

        client.RegisterMessageHandler(PackageType.EntityRpc, EntityRpcHandler);
        client.RegisterMessageHandler(PackageType.EntityRpcCallBack, EntityRpcCallBackHandler);
        client.RegisterMessageHandler(PackageType.PropertyFullSync, PropertyFullSync);
        client.RegisterMessageHandler(PackageType.PropertySyncCommandList, PropSyncCommandList);
        client.RegisterMessageHandler(PackageType.ComponentSync, ComponentSync);
        client.RegisterMessageHandler(PackageType.RequireCreateShadowEntity, RequireCreateShadow);
        client.RegisterMessageHandler(PackageType.RequireCreateShadowEntityRes, RequireCreateShadowRes);
        client.RegisterMessageHandler(PackageType.RequireDestroyShadowEntity, RequireDestroyShadow);

        Logger.Info($"client {serverIdx} registered msg");
    }

    private void UnregisterGateMessageHandlers(int idx)
    {
        var client = this.tcpClientsToServer![idx];

        client.UnregisterMessageHandler(
            PackageType.EntityRpc,
            this.tcpClientsActions[(idx, PackageType.EntityRpc)]);

        client.UnregisterMessageHandler(
            PackageType.EntityRpcCallBack,
            this.tcpClientsActions[(idx, PackageType.EntityRpcCallBack)]);

        client.UnregisterMessageHandler(
            PackageType.PropertyFullSync,
            this.tcpClientsActions[(idx, PackageType.PropertyFullSync)]);

        client.UnregisterMessageHandler(
            PackageType.PropertySyncCommandList,
            this.tcpClientsActions[(idx, PackageType.PropertySyncCommandList)]);

        client.UnregisterMessageHandler(
            PackageType.ComponentSync,
            this.tcpClientsActions[(idx, PackageType.ComponentSync)]);

        client.UnregisterMessageHandler(
            PackageType.RequireCreateShadowEntity,
            this.tcpClientsActions[(idx, PackageType.RequireCreateShadowEntity)]);

        client.UnregisterMessageHandler(
            PackageType.RequireCreateShadowEntityRes,
            this.tcpClientsActions[(idx, PackageType.RequireCreateShadowEntityRes)]);

        client.UnregisterMessageHandler(
            PackageType.RequireDestroyShadowEntity,
            this.tcpClientsActions[(idx, PackageType.RequireDestroyShadowEntity)]);
    }

    private void HandleComponentSyncFromServer(TcpClient client, (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("HandleComponentSyncFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var componentSync = (msg as ComponentSync)!;

        Logger.Info("send componentSync to client");
        this.RedirectMsgToClientEntity(componentSync.EntityId, msg);

        // Server-shadow fan-out for component sync.
        var subs = this.GetShadowSubscriptions(componentSync.EntityId);
        if (subs is not null)
        {
            foreach (var sub in subs.Keys)
            {
                sub.Send(componentSync, false);
            }
        }
    }

    private void HandlePropertySyncCommandListFromServer(
        TcpClient client,
        (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var propertySyncCommandList = (msg as PropertySyncCommandList)!;

        Logger.Info($"property sync: {propertySyncCommandList.Path}" +
                    $" {propertySyncCommandList.EntityId}" +
                    $" {propertySyncCommandList.PropType}");

        // Existing path: deliver to the owning client (untouched).
        this.RedirectMsgToClientEntity(propertySyncCommandList.EntityId, propertySyncCommandList);

        // Server-shadow fan-out: forward to any peer servers that hold a
        // shadow of this ori entity (plan D4 routing). Subscription registry
        // lives on Gate per plan D8 (see Gate.ShadowMessages.cs).
        var subs = this.GetShadowSubscriptions(propertySyncCommandList.EntityId);
        if (subs is not null)
        {
            foreach (var sub in subs.Keys)
            {
                sub.Send(propertySyncCommandList, false);
            }
        }
    }

    private void HandleEntityRpcFromServer(TcpClient client, object arg)
    {
        Logger.Info("HandleEntityRpcFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var entityRpc = (msg as EntityRpc)!;
        this.HandleEntityRpcMessageOnGate(entityRpc);
    }

    private void HandleEntityRpcCallBackFromServer(TcpClient client, (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("HandleEntityRpcCallBackFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var callBack = (msg as EntityRpcCallBack)!;
        this.HandleEntityRpcCallBackMessageOnGate(callBack);
    }

    private void HandlePropertyFullSyncFromServer(TcpClient client, object arg)
    {
        Logger.Info("HandlePropertyFullSyncFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var fullSync = (msg as PropertyFullSync)!;

        Logger.Info("send fullSync to client");
        this.RedirectMsgToClientEntity(fullSync.EntityId, msg);

        // Server-shadow fan-out for full sync (plan R2 Option B: ori-server
        // emits PropertyFullSync right after RequireCreateShadowEntityRes;
        // shadow-server's HandlePropertyFullSyncForShadow applies it).
        var subs = this.GetShadowSubscriptions(fullSync.EntityId);
        if (subs is not null)
        {
            foreach (var sub in subs.Keys)
            {
                sub.Send(fullSync, false);
            }
        }
    }

    private void RedirectMsgToEntityOnServer(string entityId, IMessage msg)
    {
        if (!this.entityIdToClientConnMapping.ContainsKey(entityId))
        {
            Logger.Warn($"{entityId} not exist!");
            return;
        }

        var mb = this.entityIdToClientConnMapping[entityId].MailBox;
        var clientToServer = this.FindServerTcpClientFromMailBox(mb);

        if (clientToServer != null)
        {
            clientToServer.Send(msg, false);
        }
        else
        {
            Logger.Warn($"gate's server client not found: {entityId}");
        }
    }
}