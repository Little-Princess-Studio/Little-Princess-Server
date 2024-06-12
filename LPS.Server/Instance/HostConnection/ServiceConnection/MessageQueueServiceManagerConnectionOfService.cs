// -----------------------------------------------------------------------
// <copyright file="MessageQueueServiceManagerConnectionOfService.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.ServiceConnection;

using System.Collections.Generic;
using LPS.Server.Instance.HostConnection;
using LPS.Server.MessageQueue;

/// <summary>
/// Represents a connection to the message queue manager for a service.
/// </summary>
public class MessageQueueServiceManagerConnectionOfService : MessageQueueManagerConnectionBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueServiceManagerConnectionOfService"/> class with the specified name.
    /// </summary>
    /// <param name="name">The name of the connection.</param>
    public MessageQueueServiceManagerConnectionOfService(string name)
        : base(name)
    {
    }

    /// <inheritdoc/>
    protected override string GetMgrExchangeName() => Consts.ServiceToServiceMgrExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr() => Consts.GenerateServiceToServiceMgrMessagePackage(this.Name);

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr() => Consts.GenerateServiceQueueName(this.Name);

    /// <inheritdoc/>
    protected override IEnumerable<string> GetDeclaringExchanges() => [Consts.ServiceToServiceMgrExchangeName, Consts.ServiceMgrToServiceExchangeName];

    /// <inheritdoc/>
    protected override bool CheckIfRoutingKeyAcceptable(string routingKey) => routingKey == Consts.GenerateServiceManagerMessageToServicePackage(this.Name);

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.ServiceMgrToServiceExchangeName,
            Consts.GetRoutingKeyFromServiceManagerToService(this.Name));
    }
}