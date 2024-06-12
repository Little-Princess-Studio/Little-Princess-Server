// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostManagerConnectionOfServiceManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System.Collections.Generic;
using LPS.Server.MessageQueue;

/// <summary>
/// Message queue host connection of service manager.
/// </summary>
public class MessageQueueHostManagerConnectionOfServiceManager : MessageQueueManagerConnectionBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueHostManagerConnectionOfServiceManager"/> class.
    /// </summary>
    public MessageQueueHostManagerConnectionOfServiceManager()
        : base("serviceMgr")
    {
    }

    /// <inheritdoc/>
    protected override string GetMgrExchangeName() => Consts.ServiceMgrToHostExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr() => Consts.ServiceMgrMessagePackage;

    /// <inheritdoc/>
    protected override IEnumerable<string> GetDeclaringExchanges() => [Consts.HostMgrToServiceMgrExchangeName, Consts.ServiceMgrToHostExchangeName];

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr() => Consts.ServiceManagerQueueName;

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        /*
         As service manager consumer, service manager should declare the binding between switch of `Consts.HostMgrToServiceMgrExchangeName`
         and message queue of `Consts.HostMgrToServiceMgrExchangeName`.
        */

        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.HostMgrToServiceMgrExchangeName,
            Consts.RoutingKeyToServiceMgr);
    }

    /// <inheritdoc/>
    protected override bool CheckIfRoutingKeyAcceptable(string routingKey) => routingKey == Consts.HostMessagePackageToServiceMgrPackage;
}