// -----------------------------------------------------------------------
// <copyright file="ImmediateHostManagerConnectionOfGate.cs" company="Little Princess Studio">
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
/// Immediate host connection of gate.
/// </summary>
internal class ImmediateHostManagerConnectionOfGate : ImmediateManagerConnectionBase
{
    private readonly string hostManagerIp;
    private readonly int hostManagerPort;
    private readonly Func<uint> onGenerateAsyncId;
    private readonly Func<MailBox?> getGateMailBox;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostManagerConnectionOfGate"/> class.
    /// </summary>
    /// <param name="hostManagerIp">Ip of the server.</param>
    /// <param name="hostManagerPort">Port of the server.</param>
    /// <param name="onGenerateAsyncId">Callback to generate async id.</param>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    /// <param name="getGateMailBox">Callback returning the gate's MailBox.
    /// May return null before initial registration completes; reconnect cannot
    /// happen before then so the null branch is defensive only.</param>
    public ImmediateHostManagerConnectionOfGate(
        string hostManagerIp,
        int hostManagerPort,
        Func<uint> onGenerateAsyncId,
        Func<bool> checkServerStopped,
        Func<MailBox?> getGateMailBox)
        : base(checkServerStopped)
    {
        this.hostManagerIp = hostManagerIp;
        this.hostManagerPort = hostManagerPort;
        this.onGenerateAsyncId = onGenerateAsyncId;
        this.getGateMailBox = getGateMailBox;
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

                this.ManagerConnectedEvent.Signal();
            },

            // On reconnect (HostManager was crashed and respawned), do NOT
            // re-send RequireCreateEntity - that would create a second
            // GateEntity. Send Control.Restart instead; HostManager.Register.cs
            // RestartInstance evicts the old connection and re-broadcasts the
            // new one. Do NOT Signal ManagerConnectedEvent (it is one-shot;
            // double-signal throws InvalidOperationException).
            OnReconnected = self =>
            {
                var mb = this.getGateMailBox();
                if (mb is null)
                {
                    Logger.Warn("[gate->host] OnReconnected fired before initial MailBox known; skipping restart-announce.");
                    return;
                }

                var restartCtl = new Control
                {
                    From = RemoteType.Gate,
                    Message = ControlMessage.Restart,
                };
                restartCtl.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(mb.Value)));
                self.Send(restartCtl, false);
                Logger.Info($"[gate->host] Reconnected; sent Control.Restart for {mb.Value}.");
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
