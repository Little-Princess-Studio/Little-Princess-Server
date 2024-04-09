// -----------------------------------------------------------------------
// <copyright file="MessageQueueServiceManagerConnectionOfService.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.ServiceConnection;

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
    protected override string GetHostMgrExchangeName()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToHostMgr()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromHostMgr()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        throw new System.NotImplementedException();
    }
}