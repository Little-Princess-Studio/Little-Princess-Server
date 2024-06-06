// -----------------------------------------------------------------------
// <copyright file="ImmediateServiceManagerConnectionOfGate.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Represents a connection to the immediate service manager of the server.
/// </summary>
internal class ImmediateServiceManagerConnectionOfGate : ImmediateManagerConnectionBase
{
    private readonly string serviceManagerIp;
    private readonly int serviceManagerPort;
    private readonly Func<uint> onGenerateAsyncId;
    private readonly Common.Rpc.MailBox gateMailBox;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateServiceManagerConnectionOfGate"/> class.
    /// </summary>
    /// <param name="serviceManagerIp">The IP address of the service manager.</param>
    /// <param name="serviceManagerPort">The port number of the service manager.</param>
    /// <param name="onGenerateAsyncId">A function that generates an asynchronous ID.</param>
    /// <param name="checkServerStopped">A function that returns a value indicating whether the server is stopped.</param>
    /// <param name="gateMailBox">The mailbox of the gate.</param>
    public ImmediateServiceManagerConnectionOfGate(
        string serviceManagerIp,
        int serviceManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped,
        Common.Rpc.MailBox gateMailBox)
        : base(checkServerStopped)
    {
        this.serviceManagerIp = serviceManagerIp;
        this.serviceManagerPort = serviceManagerPort;
        this.onGenerateAsyncId = onGenerateAsyncId;
        this.gateMailBox = gateMailBox;
    }

    /// <inheritdoc/>
    protected override TcpClient GetTcpClient() => new(
        this.serviceManagerIp,
        this.serviceManagerPort,
        new())
        {
            OnInit = self =>
            {
                self.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleMessageFromManager<ServiceRpcCallBack>);
                self.RegisterMessageHandler(PackageType.EntityRpc, this.HandleMessageFromManager<EntityRpc>);
            },
            OnConnected = self =>
            {
                this.ManagerConnectedEvent.Signal();

                var serviceCtl = new ServiceControl
                {
                    From = ServiceRemoteType.Gate,
                    Message = ServiceControlMessage.Ready,
                };

                serviceCtl.Args.Add(
                    RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(this.gateMailBox)));
                this.Send(serviceCtl);
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleMessageFromManager<ServiceRpcCallBack>);
                self.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleMessageFromManager<EntityRpc>);
            },
        };
}