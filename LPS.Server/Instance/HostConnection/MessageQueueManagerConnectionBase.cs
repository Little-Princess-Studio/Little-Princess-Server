﻿// -----------------------------------------------------------------------
// <copyright file="MessageQueueManagerConnectionBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.MessageQueue;

/// <summary>
/// Use message queue to connect to host manager.
/// </summary>
public abstract class MessageQueueManagerConnectionBase : IManagerConnection
{
    /// <summary>
    /// Name of the owner.
    /// </summary>
    protected readonly string Name;

    private readonly MessageQueueClient messageQueueClientToHostMgr;
    private readonly Dispatcher<IMessage> dispatcher = new();

    private uint rpcId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueManagerConnectionBase"/> class.
    /// </summary>
    /// <param name="name">Name of the connection, used to genereate message queue name.</param>
    public MessageQueueManagerConnectionBase(string name)
    {
        this.Name = name;
        this.messageQueueClientToHostMgr = new MessageQueueClient();
    }

    /// <inheritdoc/>
    public virtual void Run()
    {
        Logger.Debug("Start mq client for host manager.");
        this.messageQueueClientToHostMgr.Init();
        this.messageQueueClientToHostMgr.AsProducer();
        this.messageQueueClientToHostMgr.AsConsumer();

        this.messageQueueClientToHostMgr.DeclareExchange(Consts.HostMgrToServerExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.HostMgrToGateExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.HostMgrToServiceMgrExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.ServerToHostExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.GateToHostExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.ServiceMgrToHostExchangeName);

        this.InitializeBinding(this.messageQueueClientToHostMgr);

        this.messageQueueClientToHostMgr.Observe(
            this.GetMessageQueueNameToReceiveMessageFromHostMgr(),
            this.HandleHostMgrMqMessage);
    }

    /// <inheritdoc/>
    public void ShutDown()
    {
        this.messageQueueClientToHostMgr.ShutDown();
    }

    /// <inheritdoc/>
    public void WaitForExit()
    {
    }

    /// <inheritdoc/>
    public void Send(IMessage message)
    {
        this.SendInternal(message);
    }

    /// <inheritdoc/>
    public void RegisterMessageHandler(PackageType packageType, Action<IMessage> handler) =>
        this.dispatcher.Register(packageType, handler);

    /// <inheritdoc/>
    public void UnregisterMessageHandler(PackageType packageType, Action<IMessage> handler) =>
        this.dispatcher.Unregister(packageType, handler);

    /// <summary>
    /// Initialize binding between exchange and message queue.
    /// </summary>
    /// <param name="client">Mq client.</param>
    protected abstract void InitializeBinding(MessageQueueClient client);

    /// <summary>
    /// Get message queue name to observe.
    /// </summary>
    /// <returns>Name of the message queue name.</returns>
    protected abstract string GetMessageQueueNameToReceiveMessageFromHostMgr();

    /// <summary>
    /// Exchange name to send message.
    /// </summary>
    /// <returns>Name of the host exchange name.</returns>
    protected abstract string GetHostMgrExchangeName();

    /// <summary>
    /// Get routing key of the message package.
    /// </summary>
    /// <returns>Routing key.</returns>
    protected abstract string GetMessagePackageRoutingKeyToHostMgr();

    private uint GenerateRpcId() => this.rpcId++;

    private void SendInternal(IMessage message)
    {
        var pkg = PackageHelper.FromProtoBuf(message, this.GenerateRpcId());

        this.messageQueueClientToHostMgr.Publish(
            pkg.ToBytes(),
            this.GetHostMgrExchangeName(),
            this.GetMessagePackageRoutingKeyToHostMgr(),
            true);
    }

    private void HandleHostMgrMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        // TODO: customize the check condition in sub classes.
        if (routingKey == Consts.GenerateHostMessageToServerPackage(this.Name) ||
            routingKey == Consts.GenerateHostMessageToGatePackage(this.Name) ||
            routingKey == Consts.HostMessagePackageToServiceMgrPackage ||
            routingKey == Consts.HostBroadCastMessagePackageToServer ||
            routingKey == Consts.HostBroadCastMessagePackageToGate)
        {
            var pkg = PackageHelper.GetPackageFromBytes(msg);
            var type = (PackageType)pkg.Header.Type;
            var protobuf = PackageHelper.GetProtoBufObjectByType(type, pkg);
            this.dispatcher.Dispatch(type, protobuf);
        }
        else
        {
            Logger.Warn($"Invalid routing key: {routingKey}");
        }
    }
}