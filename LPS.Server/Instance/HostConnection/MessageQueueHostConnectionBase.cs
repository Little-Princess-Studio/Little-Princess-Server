// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostConnectionBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.MessageQueue;

/// <summary>
/// Use message queue to connect to host manager.
/// </summary>
public class MessageQueueHostConnectionBase : IHostConnection
{
    private readonly MessageQueueClient messageQueueClientToHostMgr;
    private readonly string name;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueHostConnectionBase"/> class.
    /// </summary>
    public MessageQueueHostConnectionBase()
    {
        Logger.Debug("Start mq client for host manager.");
        this.messageQueueClientToHostMgr = new MessageQueueClient();
        this.messageQueueClientToHostMgr.Init();
        this.messageQueueClientToHostMgr.AsProducer();
        this.messageQueueClientToHostMgr.AsConsumer();

        this.messageQueueClientToHostMgr.DeclareExchange(Consts.HostMgrToServerExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.ServerToHostExchangeName);
        this.messageQueueClientToHostMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.name),
            Consts.HostMgrToServerExchangeName,
            Consts.RoutingKeyToServer);
        this.messageQueueClientToHostMgr.Observe(
            Consts.GenerateHostManagerQueueName(this.name),
            this.HandleHostMgrMqMessage);
    }

    /// <inheritdoc/>
    public void Run()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void ShutDown()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void WaitForExit()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void Send(IMessage message)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void RegisterMessageHandler(PackageType packageType, Action<IMessage> handler)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void UnregisterMessageHandler(PackageType packageType, Action<IMessage> handler)
    {
        throw new NotImplementedException();
    }

    private void HandleHostMgrMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        if (routingKey == Consts.HostMessagePackage)
        {
        }
    }
}