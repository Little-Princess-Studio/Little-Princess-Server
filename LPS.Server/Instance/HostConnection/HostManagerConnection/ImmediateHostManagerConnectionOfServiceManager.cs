// -----------------------------------------------------------------------
// <copyright file="ImmediateHostManagerConnectionOfServiceManager.cs" company="Little Princess Studio">
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
internal class ImmediateHostManagerConnectionOfServiceManager : ImmediateManagerConnectionBase
{
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostManagerConnectionOfServiceManager"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    public ImmediateHostManagerConnectionOfServiceManager(
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
        new(
            this.hostManagerIp,
            this.hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>())
        {
            OnInit = self =>
            {
                self.RegisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromManager<HostCommand>);
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromManager<HostCommand>);
                this.MsgDispatcher.Clear();
            },
            OnConnected = self =>
            {
                this.managerConnectedEvent.Signal();
            },
        };

    /// <inheritdoc/>
    protected override void BeforeStartPumpMessage() => this.managerConnectedEvent.Wait();
}