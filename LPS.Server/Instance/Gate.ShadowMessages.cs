// -----------------------------------------------------------------------
// <copyright file="Gate.ShadowMessages.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Concurrent;
using System.Linq;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Gate-side shadow entity routing: subscription registry + fan-out for
/// property syncs + relay path for CreateShadowEntity / DestroyShadowEntity
/// between the ori-server and the target shadow-server.
/// <para>
/// Per plan D8: subscription state lives on Gate (no HostManager involvement).
/// The registry is keyed by ori MailBox.Id; the value is the set of TcpClients
/// to shadow-hosting servers that want forwards of that ori's property syncs.
/// </para>
/// <para>
/// Per plan D4: cross-server sync routes Server(ori) -> Gate -> Server(shadow)
/// reusing the existing TcpClient mesh; no new server-server channel.
/// </para>
/// </summary>
public partial class Gate
{
    // ori MailBox.Id -> set of TcpClients to shadow-hosting servers.
    // ConcurrentDictionary-as-set pattern (byte value).
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<TcpClient, byte>> shadowSubscriptions = new();

    /// <summary>
    /// Returns the set of TcpClients to shadow-servers that should receive
    /// fan-out for the given ori entity, or null if there are no shadows
    /// registered for it. Called from <c>Gate.ClientMessages.cs</c> property
    /// sync handlers to extend the existing single-target redirect with a
    /// fan-out branch.
    /// </summary>
    /// <param name="oriEntityId">Ori entity MailBox.Id.</param>
    /// <returns>Set or null.</returns>
    internal ConcurrentDictionary<TcpClient, byte>? GetShadowSubscriptions(string oriEntityId)
    {
        this.shadowSubscriptions.TryGetValue(oriEntityId, out var subs);
        return subs;
    }

    /// <summary>
    /// Receives <c>RequireCreateShadowEntity</c> from an ori-server. Computes
    /// the derived shadow MailBox, registers the subscription speculatively,
    /// forwards a <c>CreateShadowEntity</c> to the target shadow-server.
    /// The target server's <c>RequireCreateShadowEntityRes</c> is handled by
    /// <see cref="HandleRequireCreateShadowEntityResFromServer"/> below.
    /// <para>
    /// The inbound message arrives over the Gate's per-server <c>TcpClient</c>
    /// bus (Server initiates from <c>Server.tcpServer.Send(req, gateConn)</c>
    /// which round-trips back to this Gate-as-TcpClient). Replies (e.g. ok=false
    /// fast-path) flow back through the SAME TcpClient via
    /// <see cref="TcpClient.Send"/>, NOT through tcpGateServer.
    /// </para>
    /// </summary>
    /// <param name="oriClient">The Gate->ori-server TcpClient the request arrived on. Used to send the failure Res back.</param>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandleRequireCreateShadowEntityFromServer(
        TcpClient oriClient,
        (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var req = (RequireCreateShadowEntity)msg;
        var oriMb = RpcHelper.PbMailBoxToRpcMailBox(req.OriMailBox);

        // Parse target server address (ip:port:hostNum format).
        var parts = req.TargetServerName.Split(':');
        if (parts.Length != 3)
        {
            Logger.Warn($"[Gate] RequireCreateShadowEntity target '{req.TargetServerName}' not in ip:port:hostNum format.");
            return;
        }

        var targetIp = parts[0];
        var targetPort = int.Parse(parts[1]);
        var targetHostNum = int.Parse(parts[2]);

        // Locate the target server's TcpClient by Ip+Port+HostNum.
        var targetClient = this.tcpClientsToServer!
            .FirstOrDefault(c => c.MailBox.Ip == targetIp
                              && c.MailBox.Port == targetPort
                              && c.MailBox.HostNum == targetHostNum);
        if (targetClient is null)
        {
            Logger.Warn(
                $"[Gate] RequireCreateShadowEntity target '{req.TargetServerName}' not found; replying ok=false to ori.");
            var failRes = new RequireCreateShadowEntityRes
            {
                OriMailBox = req.OriMailBox,
                ShadowMailBox = req.OriMailBox, // unused on failure
                RequestId = req.RequestId,
                Ok = false,
                Error = $"target server '{req.TargetServerName}' unknown",
            };
            oriClient.Send(failRes, false);
            return;
        }

        // Derived shadow id: oriId@shadow@ip:port:hostNum.
        var shadowId = oriMb.Id + Server.ShadowIdInfix + req.TargetServerName;
        var shadowMb = new MailBox(shadowId, targetIp, targetPort, targetHostNum);

        // Register subscription speculatively. On Res ok=false (handled in
        // RequireCreateShadowEntityRes path) we roll back.
        var subs = this.shadowSubscriptions.GetOrAdd(oriMb.Id, _ => new ConcurrentDictionary<TcpClient, byte>());
        subs[targetClient] = 0;

        // Remember which ori TcpClient to reply to once target acks.
        this.pendingShadowCreates[(oriMb.Id, req.RequestId)] = (oriClient, targetClient, shadowMb);

        var fwd = new CreateShadowEntity
        {
            OriMailBox = req.OriMailBox,
            ShadowMailBox = RpcHelper.RpcMailBoxToPbMailBox(shadowMb),
            EntityClassName = req.EntityClassName,
        };
        targetClient.Send(fwd);
        Logger.Info($"[Gate] Routed CreateShadowEntity for ori={oriMb.Id} target={req.TargetServerName} shadow={shadowId}.");
    }

    /// <summary>
    /// Receives the target's <c>RequireCreateShadowEntityRes</c>. Forward to
    /// the ori-server that initiated. On ok=false, also un-register the
    /// speculative subscription we set up in HandleRequireCreateShadowEntityFromServer.
    /// </summary>
    /// <param name="targetClient">TcpClient to the target server (not used here but kept for symmetry).</param>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandleRequireCreateShadowEntityResFromServer(
        TcpClient targetClient,
        (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var res = (RequireCreateShadowEntityRes)msg;
        var oriMb = RpcHelper.PbMailBoxToRpcMailBox(res.OriMailBox);
        var shadowMb = RpcHelper.PbMailBoxToRpcMailBox(res.ShadowMailBox);

        // Correlate by (oriId, shadowId) since target server overwrites RequestId.
        var key = this.pendingShadowCreates.Keys
            .FirstOrDefault(k => k.OriId == oriMb.Id && this.pendingShadowCreates[k].Shadow.Id == shadowMb.Id);
        if (key.OriId is null)
        {
            Logger.Warn($"[Gate] RequireCreateShadowEntityRes for unknown pending ori={oriMb.Id} shadow={shadowMb.Id}.");
            return;
        }

        this.pendingShadowCreates.TryRemove(key, out var pending);
        if (!res.Ok)
        {
            if (this.shadowSubscriptions.TryGetValue(oriMb.Id, out var subs))
            {
                subs.TryRemove(pending.Target, out _);
                if (subs.IsEmpty)
                {
                    this.shadowSubscriptions.TryRemove(oriMb.Id, out _);
                }
            }
        }

        var forwarded = new RequireCreateShadowEntityRes
        {
            OriMailBox = res.OriMailBox,
            ShadowMailBox = res.ShadowMailBox,
            RequestId = key.RequestId,
            Ok = res.Ok,
            Error = res.Error,
        };
        pending.OriClient.Send(forwarded, false);
        Logger.Info($"[Gate] Relayed RequireCreateShadowEntityRes (ok={res.Ok}) for ori={oriMb.Id} shadow={shadowMb.Id} back to ori-server.");
    }

    /// <summary>
    /// Receives <c>RequireDestroyShadowEntity</c> from an ori-server. Removes
    /// subscription and forwards <c>DestroyShadowEntity</c> to the target.
    /// </summary>
    /// <param name="oriClient">TcpClient that sent the destroy.</param>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandleRequireDestroyShadowEntityFromServer(
        TcpClient oriClient,
        (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var req = (RequireDestroyShadowEntity)msg;
        var oriMb = RpcHelper.PbMailBoxToRpcMailBox(req.OriMailBox);
        var shadowMb = RpcHelper.PbMailBoxToRpcMailBox(req.ShadowMailBox);

        // Target identified by shadow's ip/port (derived in create).
        var targetClient = this.tcpClientsToServer!
            .FirstOrDefault(c => c.MailBox.Ip == shadowMb.Ip
                              && c.MailBox.Port == shadowMb.Port
                              && c.MailBox.HostNum == shadowMb.HostNum);

        if (this.shadowSubscriptions.TryGetValue(oriMb.Id, out var subs) && targetClient is not null)
        {
            subs.TryRemove(targetClient, out _);
            if (subs.IsEmpty)
            {
                this.shadowSubscriptions.TryRemove(oriMb.Id, out _);
            }
        }

        if (targetClient is null)
        {
            Logger.Warn($"[Gate] DestroyShadowEntity target unknown for shadow={shadowMb.Id}; subscription pruned anyway.");
            return;
        }

        var fwd = new DestroyShadowEntity
        {
            ShadowMailBox = req.ShadowMailBox,
        };
        targetClient.Send(fwd);
        Logger.Info($"[Gate] Routed DestroyShadowEntity for shadow={shadowMb.Id}.");
    }

    // Pending create requests waiting on target's Res. Key = (oriId, requestId).
    private readonly ConcurrentDictionary<(string OriId, uint RequestId), (TcpClient OriClient, TcpClient Target, MailBox Shadow)> pendingShadowCreates = new();
}
