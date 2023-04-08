// -----------------------------------------------------------------------
// <copyright file="ImmediateHostConnectionOfServer.cs" company="Little Princess Studio">
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
/// Server connection to host manager.
/// </summary>
internal class ImmediateHostConnectionOfServer : ImmediateHostConnectionBase
{
    private readonly CountdownEvent hostManagerConnectedEvent;
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostConnectionOfServer"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    public ImmediateHostConnectionOfServer(
        string hostManagerIp,
        int hostManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped)
        : base(checkServerStopped)
    {
        this.hostManagerIp = hostManagerIp;
        this.hostManagerPort = hostManagerPort;
        this.onGenerateAsyncId = onGenerateAsyncId;
        this.hostManagerConnectedEvent = new CountdownEvent(1);
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
                    this.HandleRequireCreateEntityResFromHost);
                self.RegisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleCreateDistributeEntity);
                self.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleRequireCreateEntityResFromHost);
                self.UnregisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleCreateDistributeEntity);
                self.UnregisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
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

    private void HandleRequireCreateEntityResFromHost((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        this.MsgDispatcher.Dispatch(PackageType.RequireCreateEntityRes, msg);
    }

    private void HandleCreateDistributeEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        this.MsgDispatcher.Dispatch(PackageType.CreateDistributeEntity, msg);
    }

    private void HandleHostCommand((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        this.MsgDispatcher.Dispatch(PackageType.HostCommand, msg);
    }
}