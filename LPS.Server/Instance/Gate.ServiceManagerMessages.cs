// -----------------------------------------------------------------------
// <copyright file="Gate.ServiceManagerMessages.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using Google.Protobuf;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Instance.HostConnection.HostManagerConnection;

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
    private void ConnectToServiceManager()
    {
        this.serviceMgrConnection = new ImmediateServiceManagerConnectionOfServer(
            this.serviceManagerMailBox.Ip,
            this.serviceManagerMailBox.Port,
            this.GenerateRpcId,
            () => this.tcpGateServer!.Stopped,
            this.entity!.MailBox);

        this.serviceMgrConnection.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.serviceMgrConnection.RegisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleEntityRpcCallBack);
        this.serviceMgrConnection.Run();
    }

    private void HandleEntityRpc(IMessage message)
    {
        var entityRpc = (EntityRpc)message;

        if (entityRpc.RpcType != RpcType.ServiceToEntity)
        {
            throw new Exception("Gate connection to service manager for EntityRpc msg only support ServiceToEntity Rpc");
        }

        // redirect entity rpc to entity's server
        this.HandleEntityRpcMessageOnGate(entityRpc);
    }

    private void HandleEntityRpcCallBack(IMessage message)
    {
        var callback = (EntityRpcCallBack)message;

        if (callback.RpcType != RpcType.EntityToService)
        {
            throw new Exception("Gate connection to service manager for EntityRpcCallBack msg only support EntityToService Rpc");
        }

        // redirect entity rpc to service manager
        this.HandleEntityRpcCallBackMessageOnGate(callback);
    }
}