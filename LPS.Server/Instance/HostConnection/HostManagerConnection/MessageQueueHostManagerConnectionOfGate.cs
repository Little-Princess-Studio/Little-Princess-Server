// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostManagerConnectionOfGate.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Message queue host connection of server.
/// </summary>
public class MessageQueueHostManagerConnectionOfGate : MessageQueueManagerConnectionBase
{
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueHostManagerConnectionOfGate"/> class.
    /// </summary>
    /// <param name="name">Unique name.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    public MessageQueueHostManagerConnectionOfGate(string name, Func<uint> onGenerateAsyncId)
        : base(name)
    {
        this.onGenerateAsyncId = onGenerateAsyncId;
    }

    /// <inheritdoc/>
    public override void Run()
    {
        base.Run();

        Logger.Debug("Send request to host");
        this.Send(
            new RequireCreateEntity
            {
                EntityType = EntityType.GateEntity,
                CreateType = CreateType.Manual,
                EntityClassName = string.Empty,
                Description = string.Empty,
                ConnectionID = this.onGenerateAsyncId.Invoke(),
            });
    }

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        /*
         As gate consumer, server should declare the binding between switch of `Consts.HostMgrToGateExchangeName`
         and message queue of `Consts.HostToGateQueueName`.
        */
        client.BindQueueAndExchange(
            this.GetMessageQueueName(),
            Consts.HostMgrToGateExchangeName,
            Consts.RoutingKeyToGate);
    }

    /// <inheritdoc/>
    protected override string GetMessageQueueName() => Consts.GenerateGateQueueName(this.Name);

    /// <inheritdoc/>
    protected override string GetHostMgrExchangeName() => Consts.GateToHostExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKey() => Consts.GenerateGateMessagePackage(this.Name);
}