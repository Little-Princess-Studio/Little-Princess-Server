// -----------------------------------------------------------------------
// <copyright file="WebMgrEndpoints.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System.Collections.Generic;

/// <summary>
/// Single source of truth for every WebManager &lt;-&gt; cluster request /
/// reply routing key pair. Replaces the scattered string constants that
/// used to live in <see cref="Consts"/>: handlers reference
/// <c>WebMgrEndpoints.X.Request</c>, the dispatcher infers
/// <c>WebMgrEndpoints.X.Reply</c> automatically.
/// <para>
/// Adding a new WebManager round-trip:
/// 1. Add an <see cref="Endpoint"/> entry here.
/// 2. Decorate a handler with <c>[WebMgrHandler(WebMgrEndpoints.X.Request)]</c>.
/// 3. Call <c>ServerService.SendWebMgrRequest(WebMgrEndpoints.X, body)</c>.
/// No other wiring required.
/// </para>
/// </summary>
public static class WebMgrEndpoints
{
    /// <summary>Pair of routing keys for a single WebManager round-trip.</summary>
    /// <param name="Request">Key WebManager publishes on (consumed by HostManager/Gate/Server/etc).</param>
    /// <param name="Reply">Key the cluster publishes back on (consumed by WebManager).</param>
    public sealed record Endpoint(string Request, string Reply);

    /// <summary>HostManager -> WebManager: count + mailboxes of all servers.</summary>
    public static readonly Endpoint ServerBasicInfo
        = new("getServerBasicInfo.toHostMgr", "serverBasicInfoRes.toWebMgr");

    /// <summary>HostManager -> WebManager: per-server ping/pong status.</summary>
    public static readonly Endpoint ServerPingPong
        = new("getServerPingPongInfo.toHostMgr", "getServerPingPongInfoRes.toWebMgr");

    /// <summary>HostManager -> WebManager: full cluster overview (gates/servers/svcmgrs/services).</summary>
    public static readonly Endpoint ClusterOverview
        = new("getClusterOverview.toHostMgr", "getClusterOverviewRes.toWebMgr");

    /// <summary>ServiceManager -> WebManager: service routing map (shards per service).</summary>
    public static readonly Endpoint ServicesList
        = new("getServiceList.toServiceMgr", "getServiceListRes.toWebMgr");

    /// <summary>Server -> WebManager: one server's detailed runtime state.</summary>
    public static readonly Endpoint ServerDetailedInfo
        = new("getServerDetailedInfo.webmgr.toSrv", "serverDetailedInfo.toWebMgr");

    /// <summary>Server -> WebManager: all entities + cells of one server.</summary>
    public static readonly Endpoint AllEntitiesOfServer
        = new("getAllEntitiesOfServer.webmgr.toSrv", "allEntitiesRes.toWebMgr");

    /// <summary>Gate -> WebManager: one gate's detailed runtime state.</summary>
    public static readonly Endpoint GateDetailedInfo
        = new("getGateDetailedInfo.webmgr.toGate", "gateDetailedInfoRes.toWebMgr");

    /// <summary>Service host -> WebManager: one shard's detailed state.</summary>
    public static readonly Endpoint ServiceShardDetailedInfo
        = new("getServiceShardDetailedInfo.webmgr.toServiceHost", "serviceShardDetailedInfoRes.toWebMgr");

    /// <summary>Every defined endpoint. Used for startup validation and dispatcher lookup.</summary>
    public static readonly IReadOnlyList<Endpoint> All = new[]
    {
        ServerBasicInfo,
        ServerPingPong,
        ClusterOverview,
        ServicesList,
        ServerDetailedInfo,
        AllEntitiesOfServer,
        GateDetailedInfo,
        ServiceShardDetailedInfo,
    };

    /// <summary>Fast request -> reply lookup. Populated once at type init.</summary>
    public static readonly IReadOnlyDictionary<string, string> RequestToReply
        = BuildRequestToReply();

    /// <summary>Set of every reply routing key. Used by WebManager to filter inbound messages.</summary>
    public static readonly IReadOnlySet<string> ReplyKeys = BuildReplyKeys();

    private static IReadOnlyDictionary<string, string> BuildRequestToReply()
    {
        var dict = new Dictionary<string, string>(All.Count);
        foreach (var e in All)
        {
            dict[e.Request] = e.Reply;
        }

        return dict;
    }

    private static IReadOnlySet<string> BuildReplyKeys()
    {
        var set = new HashSet<string>(All.Count);
        foreach (var e in All)
        {
            set.Add(e.Reply);
        }

        return set;
    }
}
