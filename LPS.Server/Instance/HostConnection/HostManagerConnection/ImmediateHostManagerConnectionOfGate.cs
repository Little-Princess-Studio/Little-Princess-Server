// -----------------------------------------------------------------------
// <copyright file="ImmediateHostManagerConnectionOfGate.cs" company="Little Princess Studio">
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
/// Immediate host connection of gate.
/// </summary>
internal class ImmediateHostManagerConnectionOfGate : ImmediateManagerConnectionBase
{
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostManagerConnectionOfGate"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    public ImmediateHostManagerConnectionOfGate(
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
    protected override TcpClient GetTcpClient()
    {
        return new TcpClient(
            this.hostManagerIp,
            this.hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>())
        {
            OnInit = self =>
            {
                self.RegisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleMessageFromManager<RequireCreateEntityRes>);
                self.RegisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromManager<HostCommand>);
                self.RegisterMessageHandler(PackageType.Ping, this.HandleMessageFromManager<Ping>);
            },
            OnConnected = self =>
            {
                self.Send(
                    new RequireCreateEntity
                    {
                        EntityType = EntityType.GateEntity,
                        CreateType = CreateType.Manual,
                        EntityClassName = string.Empty,
                        Description = string.Empty,
                        ConnectionID = this.onGenerateAsyncId.Invoke(),
                    },
                    false);

                this.managerConnectedEvent.Signal();
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleMessageFromManager<RequireCreateEntityRes>);
                self.UnregisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromManager<HostCommand>);
                self.UnregisterMessageHandler(PackageType.Ping, this.HandleMessageFromManager<Ping>);
            },
        };
    }
}