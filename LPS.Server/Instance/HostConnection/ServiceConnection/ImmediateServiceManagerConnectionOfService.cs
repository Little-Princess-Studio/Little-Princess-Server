// -----------------------------------------------------------------------
// <copyright file="ImmediateServiceManagerConnectionOfService.cs" company="Little Princess Studio">
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
internal class ImmediateServiceManagerConnectionOfService : ImmediateManagerConnectionBase
{
    private readonly string serviceManagerIp;
    private readonly int serviceManagerPort;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateServiceManagerConnectionOfService"/> class.
    /// </summary>
    /// <param name="serviceManagerIp">The IP address of the service manager.</param>
    /// <param name="serviceManagerPort">The port number of the service manager.</param>
    /// <param name="checkServerStopped">A function that returns a value indicating whether the server is stopped.</param>
    public ImmediateServiceManagerConnectionOfService(
        string serviceManagerIp,
        int serviceManagerPort,
        Func<bool> checkServerStopped)
        : base(checkServerStopped)
    {
        this.serviceManagerIp = serviceManagerIp;
        this.serviceManagerPort = serviceManagerPort;
    }

    /// <inheritdoc/>
    protected override TcpClient GetTcpClient() => new(
        this.serviceManagerIp,
        this.serviceManagerPort,
        new())
        {
            OnInit = self =>
            {
                self.RegisterMessageHandler(PackageType.ServiceManagerCommand, this.HandleMessageFromManager<ServiceManagerCommand>);
                self.RegisterMessageHandler(PackageType.ServiceRpc, this.HandleMessageFromManager<ServiceRpc>);
                self.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleMessageFromManager<ServiceRpcCallBack>);
                self.RegisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleMessageFromManager<EntityRpcCallBack>);
            },
            OnConnected = self =>
            {
                this.ManagerConnectedEvent.Signal();
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(PackageType.ServiceManagerCommand, this.HandleMessageFromManager<ServiceManagerCommand>);
                self.UnregisterMessageHandler(PackageType.ServiceRpc, this.HandleMessageFromManager<ServiceRpc>);
                self.UnregisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleMessageFromManager<ServiceRpcCallBack>);
                self.UnregisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleMessageFromManager<EntityRpcCallBack>);
            },
        };
}