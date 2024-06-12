// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostManagerConnectionOfGate.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using System.Collections.Generic;
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
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.HostMgrToGateExchangeName,
            Consts.GetRoutingKeyToGate(this.Name));

        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.HostMgrToGateExchangeName,
            Consts.HostBroadCastMessagePackageToGate);
    }

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr() => Consts.GenerateGateQueueName(this.Name);

    /// <inheritdoc/>
    protected override string GetMgrExchangeName() => Consts.GateToHostExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr() => Consts.GenerateGateMessagePackage(this.Name);

    /// <inheritdoc/>
    protected override IEnumerable<string> GetDeclaringExchanges() => [Consts.HostMgrToGateExchangeName, Consts.GateToHostExchangeName];

    /// <inheritdoc/>
    protected override bool CheckIfRoutingKeyAcceptable(string routingKey) =>
        routingKey == Consts.GenerateHostMessageToGatePackage(this.Name) || routingKey == Consts.HostBroadCastMessagePackageToGate;
}