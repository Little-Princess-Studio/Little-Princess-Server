// -----------------------------------------------------------------------
// <copyright file="MessageQueueServiceManagerConnectionOfServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.ServiceConnection;

using LPS.Server.Instance.HostConnection;
using LPS.Server.MessageQueue;

/// <summary>
/// Represents a connection to the message queue manager for a service.
/// </summary>
public class MessageQueueServiceManagerConnectionOfServer : MessageQueueManagerConnectionBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueServiceManagerConnectionOfServer"/> class with the specified name.
    /// </summary>
    /// <param name="name">The name of the connection.</param>
    public MessageQueueServiceManagerConnectionOfServer(string name)
        : base(name)
    {
    }

    /// <inheritdoc/>
    protected override string GetMgrExchangeName() => Consts.ServerToServiceMgrExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr() => Consts.GenerateServerToServiceMgrMessagePackage(this.Name);

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr() => Consts.GenerateGateQueueName(this.Name);

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.ServiceMgrToServerExchangeName,
            Consts.GetRoutingKeyFromServiceManagerToServer(this.Name));
    }
}