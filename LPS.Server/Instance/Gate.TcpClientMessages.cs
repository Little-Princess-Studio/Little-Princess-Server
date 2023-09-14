// -----------------------------------------------------------------------
// <copyright file="Gate.TcpClientMessages.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc.InnerMessages;

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
    private void RegisterMessageFromServerAndOtherGateHandlers()
    {
        this.tcpGateServer.RegisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromTcpClient);

        // tcpGateServer_.RegisterMessageHandler(PackageType.Control, this.HandleControlMessage);
        this.tcpGateServer.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromTcpClient);
        this.tcpGateServer.RegisterMessageHandler(PackageType.EntityRpcCallBack, callback: this.HandleEntityRpcCallBackFromTcpClient);
        this.tcpGateServer.RegisterMessageHandler(
            PackageType.RequirePropertyFullSync,
            this.HandleRequireFullSyncFromTcpClient);
        this.tcpGateServer.RegisterMessageHandler(
            PackageType.RequireComponentSync,
            this.HandleRequireComponentSyncFromTcpClient);
    }

    private void UnregisterMessageFromServerAndOtherGateHandlers()
    {
        this.tcpGateServer.UnregisterMessageHandler(
            PackageType.Authentication,
            this.HandleAuthenticationFromTcpClient);

        // tcpGateServer_.UnregisterMessageHandler(PackageType.Control, this.HandleControlMessage);
        this.tcpGateServer.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromTcpClient);
        this.tcpGateServer.UnregisterMessageHandler(PackageType.EntityRpcCallBack, callback: this.HandleEntityRpcCallBackFromTcpClient);
        this.tcpGateServer.UnregisterMessageHandler(
            PackageType.RequirePropertyFullSync,
            this.HandleRequireFullSyncFromTcpClient);
        this.tcpGateServer.UnregisterMessageHandler(
            PackageType.RequireComponentSync,
            this.HandleRequireComponentSyncFromTcpClient);
    }

    private void HandleEntityRpcFromTcpClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        // if gate's server have recieved the EntityRpc msg, it must be redirect from other gates
        Logger.Info("Handle EntityRpc From Other Gates.");

        var (msg, _, _) = arg;
        var entityRpc = (msg as EntityRpc)!;
        this.HandleEntityRpcMessageOnGate(entityRpc);
    }

    private void HandleEntityRpcCallBackFromTcpClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        // if gate's server have recieved the EntityRpc msg, it must be redirect from other gates
        Logger.Info("Handle EntityRpc From Other Gates.");

        var (msg, _, _) = arg;
        var callback = (msg as EntityRpcCallBack)!;
        this.HandleEntityRpcCallBackMessageOnGate(callback);
    }

    private void HandleAuthenticationFromTcpClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        var auth = (msg as Authentication)!;

        // TODO: Cache the rsa object
        var decryptedData = DecryptedCiphertext(auth);

        Logger.Info($"Got decrypted content: {decryptedData}");

        if (decryptedData == auth.Content)
        {
            Logger.Info("Auth success");

            var connId = this.GenerateRpcId();

            var createEntityMsg = new RequireCreateEntity
            {
                EntityClassName = "Untrusted",
                CreateType = CreateType.Anywhere,
                Description = string.Empty,
                EntityType = EntityType.ServerClientEntity,
                ConnectionID = connId,
                GateId = this.entity!.MailBox.Id,
            };

            if (conn.ConnectionId != uint.MaxValue)
            {
                throw new Exception("Entity is creating");
            }

            conn.ConnectionId = connId;
            this.createEntityMapping[connId] = conn;

            this.hostConnection.Send(createEntityMsg);
        }
        else
        {
            Logger.Warn("Auth failed");
            conn.Disconnect();
            conn.TokenSource.Cancel();
        }
    }

    private void HandleRequireFullSyncFromTcpClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("[Gate] HandleRequireFullSyncFromClient");
        var (msg, conn, _) = arg;
        var requirePropertyFullSyncMsg = (msg as RequirePropertyFullSync)!;

        this.RedirectMsgToEntityOnServer(requirePropertyFullSyncMsg.EntityId, msg);
    }

    private void HandleRequireComponentSyncFromTcpClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("[Gate] HandleRequireComponentSyncFromClient");
        var (msg, conn, _) = arg;
        var requireComponentSyncMsg = (msg as RequireComponentSync)!;

        this.RedirectMsgToEntityOnServer(requireComponentSyncMsg.EntityId, msg);
    }
}