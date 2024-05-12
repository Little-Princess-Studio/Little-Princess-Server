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
    protected override string GetMgrExchangeName()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    protected override void InitializeBinding(MessageQueueClient client)
    {
        throw new System.NotImplementedException();
    }
}