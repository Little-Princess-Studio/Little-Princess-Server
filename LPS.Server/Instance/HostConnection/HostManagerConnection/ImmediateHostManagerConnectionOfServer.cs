// -----------------------------------------------------------------------
// <copyright file="ImmediateHostManagerConnectionOfServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection.HostManagerConnection;

using System;
using System.Collections.Concurrent;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Server connection to host manager.
/// </summary>
internal class ImmediateHostManagerConnectionOfServer : ImmediateManagerConnectionBase
{
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;
    private readonly Func<MailBox?> getServerMailBox;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostManagerConnectionOfServer"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    /// <param name="getServerMailBox">Callback returning the server's MailBox.
    /// May return null before initial registration completes; reconnect cannot
    /// happen before then so the null branch is defensive only.</param>
    public ImmediateHostManagerConnectionOfServer(
        string hostManagerIp,
        int hostManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped,
        Func<MailBox?> getServerMailBox)
        : base(checkServerStopped)
    {
        this.hostManagerIp = hostManagerIp;
        this.hostManagerPort = hostManagerPort;
        this.onGenerateAsyncId = onGenerateAsyncId;
        this.getServerMailBox = getServerMailBox;
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
                    this.HandleMessageFromManager<RequireCreateEntityRes>);
                self.RegisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleMessageFromManager<CreateDistributeEntity>);
                self.RegisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromManager<HostCommand>);
                self.RegisterMessageHandler(PackageType.Ping, this.HandleMessageFromManager<Ping>);
            },
            OnDispose = self =>
            {
                self.UnregisterMessageHandler(
                    PackageType.RequireCreateEntityRes,
                    this.HandleMessageFromManager<RequireCreateEntityRes>);
                self.UnregisterMessageHandler(
                    PackageType.CreateDistributeEntity,
                    this.HandleMessageFromManager<CreateDistributeEntity>);
                self.UnregisterMessageHandler(PackageType.HostCommand, this.HandleMessageFromManager<HostCommand>);
                self.UnregisterMessageHandler(PackageType.Ping, this.HandleMessageFromManager<Ping>);
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

                this.ManagerConnectedEvent.Signal();
            },

            // On reconnect (HostManager respawned), do NOT re-send
            // RequireCreateEntity - that would create a second ServerEntity.
            // Send Control.Restart which hits HostManager.Register.cs
            // RestartInstance for proper re-registration. Do NOT Signal
            // ManagerConnectedEvent (one-shot CountdownEvent).
            OnReconnected = self =>
            {
                var mb = this.getServerMailBox();
                if (mb is null)
                {
                    Logger.Warn("[server->host] OnReconnected fired before initial MailBox known; skipping restart-announce.");
                    return;
                }

                var restartCtl = new Control
                {
                    From = RemoteType.Server,
                    Message = ControlMessage.Restart,
                };
                restartCtl.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(mb.Value)));
                self.Send(restartCtl, false);
                Logger.Info($"[server->host] Reconnected; sent Control.Restart for {mb.Value}.");
            },
        };
}
