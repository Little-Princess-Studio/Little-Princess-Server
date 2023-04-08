// -----------------------------------------------------------------------
// <copyright file="ImmediateHostConnectionOfGate.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using System.Collections.Concurrent;
using System.Threading;
using Google.Protobuf;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages.ProtobufDefs;

/// <summary>
/// Immediate host connection of gate.
/// </summary>
internal class ImmediateHostConnectionOfGate : ImmediateHostConnectionBase
{
    private readonly CountdownEvent hostManagerConnectedEvent;
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostConnectionOfGate"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    public ImmediateHostConnectionOfGate(
        string hostManagerIp,
        int hostManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped)
        : base(checkServerStopped)
    {
        this.hostManagerIp = hostManagerIp;
        this.hostManagerPort = hostManagerPort;
        this.hostManagerConnectedEvent = new CountdownEvent(1);
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
                    this.HandleRequireCreateEntityResFromHost);
                self.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommandFromHost);
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

                this.hostManagerConnectedEvent.Signal();
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleRequireCreateEntityResFromHost);
                self.UnregisterMessageHandler(PackageType.HostCommand, this.HandleHostCommandFromHost);
            },
        };
    }

    /// <inheritdoc/>
    protected override void BeforeStartPumpMessage() => this.hostManagerConnectedEvent.Wait();

    private void HandleRequireCreateEntityResFromHost((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        this.MsgDispatcher.Dispatch(PackageType.RequireCreateEntityRes, msg);
    }

    private void HandleHostCommandFromHost((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        this.MsgDispatcher.Dispatch(PackageType.HostCommand, msg);
    }
}