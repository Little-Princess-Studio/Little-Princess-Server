﻿// -----------------------------------------------------------------------
// <copyright file="ImmediateHostManagerConnectionOfServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using System.Collections.Concurrent;
using Google.Protobuf;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Server connection to host manager.
/// </summary>
internal class ImmediateHostManagerConnectionOfServer : ImmediateManagerConnectionBase
{
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostManagerConnectionOfServer"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    public ImmediateHostManagerConnectionOfServer(
        string hostManagerIp,
        int hostManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped)
        : base(checkServerStopped)
    {
        this.hostManagerIp = hostManagerIp;
        this.hostManagerPort = hostManagerPort;
        this.onGenerateAsyncId = onGenerateAsyncId;
    }

    /// <inheritdoc/>
    protected override TcpClient GetTcpClient() =>
        new TcpClient(
            this.hostManagerIp,
            this.hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>())
        {
            OnInit = self =>
            {
                self.RegisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleMessageFromHost<RequireCreateEntityRes>);
                self.RegisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleMessageFromHost<CreateDistributeEntity>);
                self.RegisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromHost<HostCommand>);
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleMessageFromHost<RequireCreateEntityRes>);
                self.UnregisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleMessageFromHost<CreateDistributeEntity>);
                self.UnregisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromHost<HostCommand>);
                this.MsgDispatcher.Clear();
            },
            OnConnected = self =>
            {
                self.Send(new RequireCreateEntity
                {
                    EntityType = EntityType.ServerEntity,
                    CreateType = CreateType.Manual,
                    EntityClassName = string.Empty,
                    Description = string.Empty,
                    ConnectionID = this.onGenerateAsyncId.Invoke(),
                });

                self.Send(new RequireCreateEntity
                {
                    EntityType = EntityType.ServerDefaultCellEntity,
                    CreateType = CreateType.Manual,
                    EntityClassName = string.Empty,
                    Description = string.Empty,
                    ConnectionID = this.onGenerateAsyncId.Invoke(),
                });

                this.hostManagerConnectedEvent.Signal();
            },
        };

    /// <inheritdoc/>
    protected override void BeforeStartPumpMessage() => this.hostManagerConnectedEvent.Wait();
}