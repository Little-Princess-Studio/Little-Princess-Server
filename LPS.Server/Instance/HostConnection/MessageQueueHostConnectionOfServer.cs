// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostConnectionOfServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Message queue host connection of server.
/// </summary>
public class MessageQueueHostConnectionOfServer : MessageQueueHostConnectionBase
{
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueHostConnectionOfServer"/> class.
    /// </summary>
    /// <param name="name">Unique name.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    public MessageQueueHostConnectionOfServer(string name, Func<uint> onGenerateAsyncId)
        : base(name)
    {
        this.onGenerateAsyncId = onGenerateAsyncId;
    }

    /// <inheritdoc/>
    public override void Run()
    {
        base.Run();

        Logger.Debug("Send request to host");
        this.Send(new RequireCreateEntity
        {
            EntityType = EntityType.ServerEntity,
            CreateType = CreateType.Manual,
            EntityClassName = string.Empty,
            Description = string.Empty,
            ConnectionID = this.onGenerateAsyncId.Invoke(),
        });

        this.Send(new RequireCreateEntity
        {
            EntityType = EntityType.ServerDefaultCellEntity,
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
         As server consumer, server should declare the binding between switch of `Consts.HostMgrToServerExchangeName`
         and message queue of `Consts.HostToServerQueueName`.
        */
        client.BindQueueAndExchange(
            this.GetMessageQueueName(),
            Consts.HostMgrToServerExchangeName,
            Consts.RoutingKeyToServer);
    }

    /// <inheritdoc/>
    protected override string GetMessageQueueName() => Consts.GenerateServerQueueName(this.Name);

    /// <inheritdoc/>
    protected override string GetHostMgrExchangeName() => Consts.ServerToHostExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKey() => Consts.GenerateServerMessagePackage(this.Name);
}