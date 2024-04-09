// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostManagerConnectionOfServiceManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using LPS.Server.MessageQueue;

/// <summary>
/// Message queue host connection of service manager.
/// </summary>
public class MessageQueueHostManagerConnectionOfServiceManager : MessageQueueManagerConnectionBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueHostManagerConnectionOfServiceManager"/> class.
    /// </summary>
    /// <param name="name">Unique name.</param>
    public MessageQueueHostManagerConnectionOfServiceManager()
        : base("serviceMgr")
    {
    }

    /// <inheritdoc/>
    protected override string GetHostMgrExchangeName() => Consts.ServiceMgrToHostExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToHostMgr() => Consts.ServiceMgrMessagePackage;

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromHostMgr() => Consts.ServiceManagerQueueName;

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        /*
         As service manager consumer, service manager should declare the binding between switch of `Consts.HostMgrToServiceMgrExchangeName`
         and message queue of `Consts.HostMgrToServiceMgrExchangeName`.
        */

        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromHostMgr(),
            Consts.HostMgrToServiceMgrExchangeName,
            Consts.RoutingKeyToServiceMgr);
    }
}