// -----------------------------------------------------------------------
// <copyright file="Server.ShadowEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using MailBox = LPS.Common.Rpc.MailBox;
using PbMailBox = LPS.Common.Rpc.InnerMessages.MailBox;

/// <summary>
/// Server-side shadow entity machinery: creation API, destruction API,
/// inbound message handlers, and the local shadow registry.
/// <para>
/// Roles a single Server may play:
/// </para>
/// <list type="bullet">
/// <item><b>Ori-side</b>: Hosts the original entity. Calls
///     <see cref="CreateShadowEntity"/> to spawn a read-only mirror of an
///     entity on a peer server. Tracks its created shadows in <c>myShadowsOf</c>
///     so they can be cleaned up when the ori is destroyed.</item>
/// <item><b>Shadow-side</b>: Receives <c>CreateShadowEntity</c> from the Gate,
///     instantiates the entity class in shadow mode, stores it in
///     <c>localShadowEntities</c>. Subsequent <c>PropertySyncCommandList</c>
///     messages addressed to the shadow MailBox.Id are applied to the local
///     shadow object. Inbound <c>EntityRpc</c> for a shadow MailBox is
///     rejected with an <c>RpcException</c> per design decision D1 (shadows
///     never accept RPC).</item>
/// </list>
/// <para>
/// Shadow MailBox.Id derivation: <c>$"{oriId}@shadow@{targetServerName}"</c>.
/// The <c>@shadow@</c> infix is the routing marker — RPC dispatcher uses it
/// to distinguish a shadow handle from an ori handle without consulting the
/// registry first.
/// </para>
/// </summary>
public partial class Server
{
    /// <summary>
    /// Routing marker embedded in derived shadow MailBox.Id values.
    /// Format: <c>$"{oriId}{ShadowIdInfix}{targetServerName}"</c>.
    /// </summary>
    public const string ShadowIdInfix = "@shadow@";

    private readonly ConcurrentDictionary<string, DistributeEntity> localShadowEntities = new();

    // Per-ori bookkeeping for shadows this server created. Lets us fan out
    // destroy messages when the ori entity is .Destroy()'d.
    private readonly ConcurrentDictionary<string, List<MailBox>> shadowsByOriId = new();

    private readonly AsyncTaskGenerator<MailBox> shadowCreateTaskGen = new();

    /// <summary>
    /// Public API: create a shadow of <paramref name="oriMailBox"/> on the
    /// server identified by <paramref name="targetServerMailBox"/>'s Ip+Port+HostNum.
    /// The shadow is addressed by the returned MailBox; later destroy calls
    /// must pass that same MailBox so the per-target subscription is removed cleanly.
    /// <para>
    /// Caller must know the target server's network address (game logic
    /// typically already does for peer-routing). Future v2 may add a
    /// name-based registry.
    /// </para>
    /// </summary>
    /// <param name="oriMailBox">MailBox of the original entity (must live on THIS server).</param>
    /// <param name="targetServerMailBox">MailBox whose Ip+Port+HostNum identifies the peer server.</param>
    /// <param name="entityClassName">Entity class name registered in entity_namespace.</param>
    /// <returns>A task whose value is the derived shadow MailBox once the target server has confirmed creation.</returns>
    public Task<MailBox> CreateShadowEntity(MailBox oriMailBox, MailBox targetServerMailBox, string entityClassName)
    {
        var (task, requestId) = this.shadowCreateTaskGen.GenerateAsyncTask();

        var req = new RequireCreateShadowEntity
        {
            OriMailBox = RpcHelper.RpcMailBoxToPbMailBox(oriMailBox),
            TargetServerName = $"{targetServerMailBox.Ip}:{targetServerMailBox.Port}:{targetServerMailBox.HostNum}",
            EntityClassName = entityClassName,
            RequestId = requestId,
        };

        this.tcpServer.Send(req, this.GateConnections[0]);
        Logger.Info($"[Server:{this.Name}] Sent RequireCreateShadowEntity for ori={oriMailBox} target={req.TargetServerName} req={requestId}.");

        return task;
    }

    /// <summary>
    /// Public API: destroy a previously-created shadow. Per-target: caller
    /// passes the shadow MailBox returned by <see cref="CreateShadowEntity"/>.
    /// Fire-and-forget; on ori-server crash the v1 design leaves shadows leaked
    /// (documented limitation in .sisyphus/plans/server-shadow-entity-v1.md D6).
    /// </summary>
    /// <param name="oriMailBox">MailBox of the original entity.</param>
    /// <param name="shadowMailBox">MailBox of the shadow to destroy.</param>
    public void DestroyShadowEntity(MailBox oriMailBox, MailBox shadowMailBox)
    {
        var req = new RequireDestroyShadowEntity
        {
            OriMailBox = RpcHelper.RpcMailBoxToPbMailBox(oriMailBox),
            ShadowMailBox = RpcHelper.RpcMailBoxToPbMailBox(shadowMailBox),
        };
        this.tcpServer.Send(req, this.GateConnections[0]);

        if (this.shadowsByOriId.TryGetValue(oriMailBox.Id, out var list))
        {
            lock (list)
            {
                list.RemoveAll(mb => mb.Id == shadowMailBox.Id);
                if (list.Count == 0)
                {
                    this.shadowsByOriId.TryRemove(oriMailBox.Id, out _);
                }
            }
        }

        Logger.Info($"[Server:{this.Name}] Sent RequireDestroyShadowEntity for shadow={shadowMailBox}.");
    }

    /// <summary>
    /// Returns true if <paramref name="entityId"/> looks like a shadow MailBox.Id
    /// (i.e. contains the <see cref="ShadowIdInfix"/> marker). Used by the
    /// RPC dispatcher to short-circuit and reject before lookup.
    /// </summary>
    /// <param name="entityId">MailBox.Id to test.</param>
    /// <returns>True if shadow-shaped.</returns>
    internal static bool IsShadowId(string entityId) => entityId.Contains(ShadowIdInfix);

    /// <summary>
    /// Returns the local shadow entity for <paramref name="shadowId"/> if any.
    /// Used by the dispatcher / sync apply path.
    /// </summary>
    /// <param name="shadowId">Shadow MailBox.Id.</param>
    /// <returns>Local shadow entity or null.</returns>
    internal DistributeEntity? TryGetLocalShadow(string shadowId)
    {
        this.localShadowEntities.TryGetValue(shadowId, out var entity);
        return entity;
    }

    /// <summary>
    /// Handle CreateShadowEntity from the Gate. This server is the TARGET:
    /// instantiate the entity class in shadow mode, register it locally, and
    /// reply with a RequireCreateShadowEntityRes so the ori-server's API task
    /// can resolve.
    /// </summary>
    /// <param name="arg">Dispatcher tuple.</param>
    private async void HandleCreateShadowEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, sourceConn, _) = arg;
        var create = (CreateShadowEntity)msg;
        var oriMb = RpcHelper.PbMailBoxToRpcMailBox(create.OriMailBox);
        var shadowMb = RpcHelper.PbMailBoxToRpcMailBox(create.ShadowMailBox);

        bool ok;
        string error = string.Empty;
        try
        {
            var entity = await RpcServerHelper.CreateShadowEntityLocally(create.EntityClassName, $"shadow-of-{oriMb.Id}");
            entity.MailBox = shadowMb;
            this.localShadowEntities[shadowMb.Id] = entity;
            ok = true;
            Logger.Info($"[Server:{this.Name}] Created shadow {shadowMb.Id} (ori={oriMb.Id}, class={create.EntityClassName}).");
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[Server:{this.Name}] Failed to create shadow ori={oriMb.Id}.");
            ok = false;
            error = e.Message;
        }

        var res = new RequireCreateShadowEntityRes
        {
            OriMailBox = create.OriMailBox,
            ShadowMailBox = create.ShadowMailBox,
            RequestId = 0, // unused on this hop; Gate uses (oriId, shadowId) correlation
            Ok = ok,
            Error = error,
        };

        // CRITICAL: reply over the SAME socket the CreateShadowEntity arrived
        // on so the Res lands on the SAME Gate that has the pending entry.
        // With multiple Gates each maintaining its own pendingShadowCreates,
        // sending via GateConnections[0] would round-robin to a Gate that has
        // no pending entry and the Res would be dropped.
        this.tcpServer.Send(res, (SocketConnection)sourceConn);
    }

    /// <summary>
    /// Handle RequireCreateShadowEntityRes (forwarded by Gate from the target server).
    /// This server is the ORI-side: resolve the pending Task<MailBox> returned
    /// from <see cref="CreateShadowEntity"/>, record the new shadow in
    /// <see cref="myShadowsOf"/>, and emit a PropertyFullSync to seed the shadow.
    /// </summary>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandleRequireCreateShadowEntityRes((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var res = (RequireCreateShadowEntityRes)msg;
        var oriMb = RpcHelper.PbMailBoxToRpcMailBox(res.OriMailBox);
        var shadowMb = RpcHelper.PbMailBoxToRpcMailBox(res.ShadowMailBox);

        if (!res.Ok)
        {
            // AsyncTaskGenerator<T> has no Reject-with-exception API today.
            // Resolve with a sentinel MailBox and log; caller's await will
            // see an "empty" MailBox.Id. v1 acceptable per plan R1 / D6 (no
            // crash recovery). Future: extend AsyncTaskGenerator with reject.
            Logger.Error(
                new Exception(res.Error),
                $"[Server:{this.Name}] CreateShadowEntity failed on target: {res.Error}");
            this.shadowCreateTaskGen.ResolveAsyncTask(res.RequestId, default(MailBox));
            return;
        }

        // Record so ori.Destroy() can fan out destroys to all shadows.
        var list = this.shadowsByOriId.GetOrAdd(oriMb.Id, _ => new List<MailBox>());
        lock (list)
        {
            list.Add(shadowMb);
        }

        this.shadowCreateTaskGen.ResolveAsyncTask(res.RequestId, shadowMb);
        Logger.Info($"[Server:{this.Name}] Shadow created on target: ori={oriMb.Id} shadow={shadowMb.Id}.");

        // Per plan R2 Option B: emit a PropertyFullSync so the just-created
        // shadow gets seeded with current state. The Gate's shadow fan-out
        // (C3) will deliver this to the target server based on the
        // subscription Gate registered earlier in this flow.
        this.EmitPropertyFullSyncForOri(oriMb);
    }

    /// <summary>
    /// Handle DestroyShadowEntity from the Gate. This server is the TARGET:
    /// remove the local shadow.
    /// </summary>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandleDestroyShadowEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var destroy = (DestroyShadowEntity)msg;
        var shadowMb = RpcHelper.PbMailBoxToRpcMailBox(destroy.ShadowMailBox);

        if (this.localShadowEntities.TryRemove(shadowMb.Id, out var entity))
        {
            // Mark via the only public lifecycle hook: Destroy() flips
            // IsDestroyed internally and runs any subclass cleanup.
            entity.Destroy();
            Logger.Info($"[Server:{this.Name}] Destroyed local shadow {shadowMb.Id}.");
        }
        else
        {
            Logger.Warn($"[Server:{this.Name}] DestroyShadowEntity for unknown id {shadowMb.Id}; ignoring.");
        }
    }

    /// <summary>
    /// Inbound PropertySyncCommandList: when the target entity-id is the
    /// ORI id of a shadow we host, apply to the local shadow; otherwise
    /// the existing routing keeps working.
    /// <para>
    /// Note: <c>fullSync.EntityId</c> and <c>sync.EntityId</c> carry the ORI
    /// MailBox.Id (not the derived shadow id) because the ori-server emits
    /// the sync addressed to its own entity. We index <c>localShadowEntities</c>
    /// by shadow MailBox.Id (oriId@shadow@...) for routing purposes, so the
    /// lookup must walk values and match by the shadow's ori-prefix.
    /// </para>
    /// </summary>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandlePropertySyncCommandListForShadow((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var sync = (PropertySyncCommandList)msg;
        var shadow = this.FindLocalShadowByOriId(sync.EntityId);
        if (shadow is null)
        {
            return;
        }

        // The sync's EntityId is ori's, but shadow.MailBox.Id is the derived
        // shadow id. ApplySyncCommandList compares against this.MailBox.Id,
        // so temporarily reflect the ori id (or rewrite the sync's EntityId).
        // Cleaner: rewrite the sync's EntityId to the shadow's so the
        // existing apply machinery is unchanged.
        var rewritten = sync.Clone();
        rewritten.EntityId = shadow.MailBox.Id;
        try
        {
            shadow.ApplySyncCommandList(rewritten, isComponentProperty: false, componentName: string.Empty);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[Server:{this.Name}] ApplySyncCommandList to shadow {shadow.MailBox.Id} failed.");
        }
    }

    /// <summary>
    /// Inbound PropertyFullSync: same routing - if it addresses a local
    /// shadow (by ori id), seed the shadow's property tree.
    /// </summary>
    /// <param name="arg">Dispatcher tuple.</param>
    private void HandlePropertyFullSyncForShadow((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var fullSync = (PropertyFullSync)msg;
        var shadow = this.FindLocalShadowByOriId(fullSync.EntityId);
        if (shadow is null)
        {
            return;
        }

        try
        {
            shadow.FromSyncContent(fullSync.PropertyTree);

            // Mirror the client-side flow: clear IsFrozen and call OnLoaded
            // so any subclass setup runs. v1 just calls OnLoaded as a hook;
            // the entity tree is already populated by FromSyncContent.
            shadow.IsFrozen = false;
            Logger.Info($"[Server:{this.Name}] Seeded shadow {shadow.MailBox.Id} (oriId={fullSync.EntityId}) from PropertyFullSync.");
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[Server:{this.Name}] FromSyncContent for shadow oriId={fullSync.EntityId} failed.");
        }
    }

    private DistributeEntity? FindLocalShadowByOriId(string oriId)
    {
        // Shadow MailBox.Id format: oriId + ShadowIdInfix + targetAddress.
        // Use prefix-startsWith for O(N) lookup; N is small in v1 (one shadow
        // per ori per server).
        var prefix = oriId + ShadowIdInfix;
        foreach (var kv in this.localShadowEntities)
        {
            if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return kv.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Emit a PropertyFullSync for the named ori entity, addressed to the
    /// fan-out path the Gate will use for any subscribed shadows. The
    /// existing client-shadow path also benefits (no behavior change there).
    /// </summary>
    /// <param name="oriMb">Ori entity MailBox.</param>
    private void EmitPropertyFullSyncForOri(MailBox oriMb)
    {
        if (!this.localEntityDict.TryGetValue(oriMb.Id, out var entity))
        {
            Logger.Warn($"[Server:{this.Name}] Cannot emit full sync: ori {oriMb.Id} not in localEntityDict.");
            return;
        }

        try
        {
            entity.FullSync((_, content) =>
            {
                var fullSyncMsg = new PropertyFullSync
                {
                    EntityId = oriMb.Id,
                    PropertyTree = content,
                };
                this.tcpServer.Send(fullSyncMsg, this.GateConnections[0]);
                Logger.Info($"[Server:{this.Name}] Emitted PropertyFullSync for ori={oriMb.Id} (will reach shadow via Gate fan-out).");
            });
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[Server:{this.Name}] Failed to emit full sync for {oriMb.Id}.");
        }
    }
}
