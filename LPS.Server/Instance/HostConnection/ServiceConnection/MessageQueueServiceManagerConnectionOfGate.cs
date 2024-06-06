// -----------------------------------------------------------------------
// <copyright file="MessageQueueServiceManagerConnectionOfGate.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.ServiceConnection;

using LPS.Server.Instance.HostConnection;
using LPS.Server.MessageQueue;

/// <summary>
/// Represents a connection to the message queue manager for a service.
/// </summary>
public class MessageQueueServiceManagerConnectionOfGate : MessageQueueManagerConnectionBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueServiceManagerConnectionOfGate"/> class with the specified name.
    /// </summary>
    /// <param name="name">The name of the connection.</param>
    public MessageQueueServiceManagerConnectionOfGate(string name)
        : base(name)
    {
    }

    /// <inheritdoc/>
    protected override string GetMgrExchangeName() => Consts.GateToServiceMgrExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr() => Consts.GenerateGateToServiceMgrMessagePackage(this.Name);

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr() => Consts.GenerateGateQueueName(this.Name);

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.ServiceMgrToGateExchangeName,
            Consts.GetRoutingKeyFromServiceManagerToGate(this.Name));
    }
}