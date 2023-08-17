// -----------------------------------------------------------------------
// <copyright file="ImmediateServiceManagerConnectionOfServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Represents a connection to the immediate service manager of the server.
/// </summary>
internal class ImmediateServiceManagerConnectionOfServer : ImmediateManagerConnectionBase
{
    private readonly string serviceManagerIp;
    private readonly int serviceManagerPort;
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateServiceManagerConnectionOfServer"/> class.
    /// </summary>
    /// <param name="serviceManagerIp">The IP address of the service manager.</param>
    /// <param name="serviceManagerPort">The port number of the service manager.</param>
    /// <param name="onGenerateAsyncId">A function that generates an asynchronous ID.</param>
    /// <param name="checkServerStopped">A function that returns a value indicating whether the server is stopped.</param>
    public ImmediateServiceManagerConnectionOfServer(
        string serviceManagerIp,
        int serviceManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped)
        : base(checkServerStopped)
    {
        this.serviceManagerIp = serviceManagerIp;
        this.serviceManagerPort = serviceManagerPort;
        this.onGenerateAsyncId = onGenerateAsyncId;
    }

    /// <inheritdoc/>
    protected override void BeforeStartPumpMessage() => this.managerConnectedEvent.Wait();

    /// <inheritdoc/>
    protected override TcpClient GetTcpClient() => new(
        this.serviceManagerIp,
        this.serviceManagerPort,
        new())
        {
            OnInit = self =>
            {
                self.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleMessageFromManager<ServiceRpcCallBack>);
            },
            OnConnected = self =>
            {
                this.managerConnectedEvent.Signal();
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleMessageFromManager<ServiceRpcCallBack>);
            },
        };
}