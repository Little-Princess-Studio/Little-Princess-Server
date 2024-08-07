﻿// -----------------------------------------------------------------------
// <copyright file="MessageQueueManagerConnectionBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using System.Collections.Generic;
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
    protected MessageQueueManagerConnectionBase(string name)
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

        var list = this.GetDeclaringExchanges();
        foreach (var name in list)
        {
            this.messageQueueClientToHostMgr.DeclareExchange(name);
        }

        this.InitializeBinding(this.messageQueueClientToHostMgr);

        this.messageQueueClientToHostMgr.Observe(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
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
    protected abstract string GetMessageQueueNameToReceiveMessageFromMgr();

    /// <summary>
    /// Exchange name to send message.
    /// </summary>
    /// <returns>Name of the host exchange name.</returns>
    protected abstract string GetMgrExchangeName();

    /// <summary>
    /// Get routing key of the message package.
    /// </summary>
    /// <returns>Routing key.</returns>
    protected abstract string GetMessagePackageRoutingKeyToMgr();

    /// <summary>
    /// Get declaring exchange names.
    /// </summary>
    /// <returns>The declaring exchange names.</returns>
    protected abstract IEnumerable<string> GetDeclaringExchanges();

    /// <summary>
    /// Check if the routingkey is acceptable for this connection.
    /// </summary>
    /// <param name="routingKey">Routing key.</param>
    /// <returns>Acceptable if true otherwise false.</returns>
    protected abstract bool CheckIfRoutingKeyAcceptable(string routingKey);

    private uint GenerateRpcId() => this.rpcId++;

    private void SendInternal(IMessage message)
    {
        var pkg = PackageHelper.FromProtoBuf(message, this.GenerateRpcId());

        this.messageQueueClientToHostMgr.Publish(
            pkg.ToBytes(),
            this.GetMgrExchangeName(),
            this.GetMessagePackageRoutingKeyToMgr(),
            true);
    }

    private void HandleHostMgrMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        if (this.CheckIfRoutingKeyAcceptable(routingKey))
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