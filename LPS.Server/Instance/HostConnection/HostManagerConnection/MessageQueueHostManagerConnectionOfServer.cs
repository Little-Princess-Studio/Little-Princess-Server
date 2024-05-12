// -----------------------------------------------------------------------
// <copyright file="MessageQueueHostManagerConnectionOfServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Message queue host connection of server.
/// </summary>
public class MessageQueueHostManagerConnectionOfServer : MessageQueueManagerConnectionBase
{
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueHostManagerConnectionOfServer"/> class.
    /// </summary>
    /// <param name="name">Unique name.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    public MessageQueueHostManagerConnectionOfServer(string name, Func<uint> onGenerateAsyncId)
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
            EntityType = EntityType.ServerDefaultCellEntity,
            CreateType = CreateType.Manual,
            EntityClassName = string.Empty,
            Description = string.Empty,
            ConnectionID = this.onGenerateAsyncId.Invoke(),
        });

        this.Send(new RequireCreateEntity
        {
            EntityType = EntityType.ServerEntity,
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
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.HostMgrToServerExchangeName,
            Consts.GetRoutingKeyToServer(this.Name));

        client.BindQueueAndExchange(
            this.GetMessageQueueNameToReceiveMessageFromMgr(),
            Consts.HostMgrToServerExchangeName,
            Consts.HostBroadCastMessagePackageToServer);
    }

    /// <inheritdoc/>
    protected override string GetMessageQueueNameToReceiveMessageFromMgr() => Consts.GenerateServerQueueName(this.Name);

    /// <inheritdoc/>
    protected override string GetMgrExchangeName() => Consts.ServerToHostExchangeName;

    /// <inheritdoc/>
    protected override string GetMessagePackageRoutingKeyToMgr() => Consts.GenerateServerMessagePackage(this.Name);
}