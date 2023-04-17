// -----------------------------------------------------------------------
// <copyright file="MessageQueueClient.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using System.Text;
using LPS.Common.Debug;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

/// <summary>
/// Client for observing and publishing message via RabbitMQ.
/// </summary>
public class MessageQueueClient : IDisposable
{
    private static readonly ConnectionFactory Factory = new ConnectionFactory()
    {
        HostName = "52.175.74.209",
        Port = 5672,
        UserName = "demo",
        Password = "123456",
    };

    private IConnection? connection;
    private IModel? producerChannel;
    private IModel? consumerChannel;

    /// <summary>
    /// Init message queue setting.
    /// </summary>
    public void Init()
    {
        this.connection = Factory.CreateConnection();
    }

    /// <summary>
    /// Declare this client act as consumer.
    /// </summary>
    public void AsConsumer()
    {
        this.consumerChannel = this.connection !.CreateModel();
    }

    /// <summary>
    /// Declare this client act as producer.
    /// </summary>
    public void AsProducer()
    {
        this.producerChannel = this.connection !.CreateModel();
        this.producerChannel.BasicReturn += (sender, args) =>
        {
            Logger.Info($"Message publish failed ({args.Exchange}, {args.RoutingKey}), retry...");
            var body = args.Body;
            this.Publish(body, args.Exchange, args.RoutingKey, true);
        };
    }

    /// <summary>
    /// Declare an exchange.
    /// </summary>
    /// <param name="exchange">Name of the exchange.</param>
    public void DeclareExchange(string exchange)
    {
        this.producerChannel!.ExchangeDeclare(exchange, ExchangeType.Topic, true);
    }

    /// <summary>
    /// Publish a message via message queue.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="exchange">Name of the exchange.</param>
    /// <param name="routingKey">Routing key.</param>
    public void Publish(string message, string exchange, string routingKey)
    {
        var body = Encoding.UTF8.GetBytes(message);
        this.producerChannel!.BasicPublish(exchange, routingKey, null, body);
    }

    /// <summary>
    /// Publish a message via message queue.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="exchange">Name of the exchange.</param>
    /// <param name="routingKey">Routing key.</param>
    /// <param name="mandatory"><see cref="IModel"/>.</param>
    public void Publish(ReadOnlyMemory<byte> message, string exchange, string routingKey, bool mandatory = false)
    {
        this.producerChannel!.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: mandatory,
            basicProperties: null,
            body: message);
    }

    /// <summary>
    /// Bind message queue and exchange.
    /// </summary>
    /// <param name="queueName">Name of the message queue.</param>
    /// <param name="exchange">Name of the exchange.</param>
    /// <param name="routingKey">Name of the observed routing key.</param>
    public void BindQueueAndExchange(string queueName, string exchange, string routingKey)
    {
        this.consumerChannel!.QueueDeclare(queueName, true, false, false, null);
        this.consumerChannel!.QueueBind(queueName, exchange, routingKey);
    }

    /// <summary>
    /// Observe a message queue.
    /// </summary>
    /// <param name="queueName">Name of the message queue.</param>
    /// <param name="callback">Callback when getting message.</param>
    public void Observe(string queueName, Action<string, string> callback)
    {
        var consumer = new EventingBasicConsumer(this.consumerChannel);
        consumer.Received += (_, eventArgs) =>
        {
            var body = eventArgs.Body;
            var message = Encoding.UTF8.GetString(body.ToArray());
            callback.Invoke(message, eventArgs.RoutingKey);
        };
        this.consumerChannel.BasicConsume(queueName, autoAck: true, consumer);
    }

    /// <summary>
    /// Observe a message queue.
    /// </summary>
    /// <param name="queueName">Name of the message queue.</param>
    /// <param name="callback">Callback when getting message.</param>
    public void Observe(string queueName, Action<ReadOnlyMemory<byte>, string> callback)
    {
        var consumer = new EventingBasicConsumer(this.consumerChannel);
        consumer.Received += (_, eventArgs) =>
        {
            var body = eventArgs.Body;
            callback.Invoke(body, eventArgs.RoutingKey);
        };
        this.consumerChannel.BasicConsume(queueName, autoAck: true, consumer);
    }

    /// <summary>
    /// Close the client.
    /// </summary>
    public void ShutDown()
    {
        this.producerChannel?.Close();
        this.consumerChannel?.Close();
        this.connection?.Close();
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        this.producerChannel?.Dispose();
        this.consumerChannel?.Dispose();
        this.connection?.Dispose();
    }
}