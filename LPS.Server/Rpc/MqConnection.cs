// -----------------------------------------------------------------------
// <copyright file="MqConnection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using System;
using Google.Protobuf;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.MessageQueue;

/// <summary>
/// Socket connection object represents a remote connection using mq to connect to remote.
/// </summary>
public class MqConnection : Connection
{
    /// <summary>
    /// Gets or sets method to generate a unique rpc id.
    /// </summary>
    public static Func<uint>? OnGenerateRpcId { get; set; }

    private readonly MessageQueueClient client;
    private readonly string exchangeName;
    private readonly string routingKey;

    /// <summary>
    /// Create a connection.
    /// </summary>
    /// <param name="client">Message queue client of the connection.</param>
    /// <param name="exchangeName">Exchange name of the mq client to send message.</param>
    /// <param name="routingKey">Routing key of the mq client to send message.</param>
    /// <returns>Connection.</returns>
    public static MqConnection Create(MessageQueueClient client, string exchangeName, string routingKey)
    {
        return new MqConnection(client, exchangeName, routingKey);
    }

    private MqConnection(MessageQueueClient client, string exchangeName, string routingKey)
    {
        this.client = client;
        this.exchangeName = exchangeName;
        this.routingKey = routingKey;
    }

    /// <inheritdoc/>
    public override void Disconnect()
    {
    }

    /// <inheritdoc/>
    public override void Send(IMessage message)
    {
        var rpcId = OnGenerateRpcId?.Invoke() ?? throw new Exception("OnGenerateRpcId is null");
        var pkg = PackageHelper.FromProtoBuf(message, rpcId);
        this.client.Publish(pkg.ToBytes(), this.exchangeName, this.routingKey);
    }

    /// <inheritdoc/>
    public override void Send(ReadOnlyMemory<byte> bytes)
    {
        this.client.Publish(bytes, this.exchangeName, this.routingKey);
    }
}