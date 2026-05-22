// -----------------------------------------------------------------------
// <copyright file="HostManager.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System.Linq;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json.Linq;

/// <summary>
/// HostManager watches the lifecycle of every Server/Gate/ServiceManager
/// and is queryable by the WebManager via the declarative
/// <see cref="WebMgrDispatcher"/>.
/// </summary>
public partial class HostManager : IInstance
{
    private WebMgrDispatcher webMgrDispatcher = null!;

    private void InitMessageQueueClientToWebManager()
    {
        Logger.Debug("Start mq client for web manager (hostmanager).");
        this.webMgrDispatcher = new WebMgrDispatcher(this.Name, this.Name);
        this.webMgrDispatcher.ScanAndRegister(this);
        this.webMgrDispatcher.Init(Consts.RoutingKeyToHostManager);
    }

    [WebMgrHandler("getServerBasicInfo.toHostMgr")]
    private JToken HandleGetServerBasicInfo(JToken body)
    {
        _ = body;
        return new JObject
        {
            ["serverCnt"] = this.DesiredServerNum,
            ["serverMailBoxes"] = new JArray(this.serversMailBoxes.Select(conn => new JObject
            {
                ["id"] = conn.Id,
                ["ip"] = conn.Ip,
                ["port"] = conn.Port,
                ["hostNum"] = conn.HostNum,
            })),
        };
    }

    [WebMgrHandler("getServerPingPongInfo.toHostMgr")]
    private JToken HandleGetServerPingPongInfo(JToken body)
    {
        _ = body;
        return new JObject
        {
            ["srvPingPongInfo"] = new JArray(
                this.serversMailBoxes
                    .Where(mb => this.instanceStatusManager.HasInstance(mb))
                    .Select(mb => this.instanceStatusManager.GetStatus(mb))
                    .Select(status => new JObject
                    {
                        ["id"] = status.MailBox.Id,
                        ["status"] = (int)status.Status,
                    })),
        };
    }

    [WebMgrHandler("getClusterOverview.toHostMgr")]
    private JToken HandleGetClusterOverview(JToken body)
    {
        _ = body;
        return this.BuildClusterOverview();
    }

    /// <summary>
    /// WebManager-initiated graceful shutdown of one cluster instance.
    /// Body shape: <c>{ instanceType, instanceId, timeoutMs? }</c>.
    /// <para>
    /// Routing:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Gate / Server / ServiceManager - HostManager sends a
    /// <see cref="HostCommandType.ShutdownInstance"/> HostCommand directly via
    /// TCP if a live <see cref="Connection"/> is held, or via the appropriate
    /// HostMgr-&gt;{Instance} MQ exchange otherwise.</description></item>
    /// <item><description>Service - HostManager forwards the request to
    /// ServiceManager (HostCommand on the HostMgr-&gt;ServiceMgr exchange).
    /// ServiceManager unpacks the target service host name from
    /// <c>HostCommand.Args[0]</c> and relays a
    /// <see cref="ServiceManagerCommandType.ShutdownInstance"/> downstream.</description></item>
    /// <item><description>DbManager - HostManager sends a HostCommand on the
    /// DbMgr-&gt;HostMgr request channel; DbManager subscribes for the new
    /// PackageType.HostCommand handler added alongside this feature.</description></item>
    /// <item><description>HostManager itself - rejected (would orphan the
    /// control plane).</description></item>
    /// </list>
    /// </summary>
    [WebMgrHandler("shutdownInstance.toHostMgr")]
    private JToken HandleShutdownInstance(JToken body)
    {
        var instanceType = body["instanceType"]?.ToString() ?? string.Empty;
        var instanceId = body["instanceId"]?.ToString() ?? string.Empty;
        var timeoutMs = body["timeoutMs"]?.ToObject<int>() ?? 0;

        Logger.Info($"[hostmgr] WebManager requested shutdown: type={instanceType} id={instanceId} timeoutMs={timeoutMs}");

        if (string.IsNullOrEmpty(instanceType) || string.IsNullOrEmpty(instanceId))
        {
            return new JObject
            {
                ["accepted"] = false,
                ["reason"] = "instanceType and instanceId are required.",
            };
        }

        if (instanceType.Equals("HostManager", System.StringComparison.OrdinalIgnoreCase))
        {
            return new JObject
            {
                ["accepted"] = false,
                ["reason"] = "Refusing to shut down HostManager via WebManager (would orphan the cluster).",
            };
        }

        // Build the HostCommand once - same payload regardless of transport.
        var hostCmd = new HostCommand
        {
            Type = HostCommandType.ShutdownInstance,
            ShutdownTimeoutMs = timeoutMs,
        };

        // For Service we need to tell ServiceManager which Service host to
        // target. Pack the id into Args[0] - ServiceManager will pop it and
        // relay a ServiceManagerCommand.ShutdownInstance.
        if (instanceType.Equals("Service", System.StringComparison.OrdinalIgnoreCase))
        {
            hostCmd.Args.Add(RpcHelper.GetRpcAny(instanceId));
        }

        var pkg = PackageHelper.FromProtoBuf(hostCmd, ServerGlobal.GenerateRpcId());
        var bytes = pkg.ToBytes();

        // Direct TCP send if HostManager still holds a live socket to the target.
        if (this.mailboxIdToConnection.TryGetValue(instanceId, out var directConn))
        {
            Logger.Info($"[hostmgr] Direct TCP send shutdown to {instanceType}:{instanceId}");
            directConn.Send(bytes);
            return new JObject { ["accepted"] = true, ["transport"] = "tcp" };
        }

        // MQ fallback path - pick the exchange + routing-key for this instance type.
        var (exchange, routingKey) = this.ResolveShutdownMqRouting(instanceType, instanceId);
        if (exchange is null || routingKey is null)
        {
            return new JObject
            {
                ["accepted"] = false,
                ["reason"] = $"Unsupported instanceType '{instanceType}' or unknown target.",
            };
        }

        Logger.Info($"[hostmgr] MQ publish shutdown to {instanceType}:{instanceId} via {exchange}/{routingKey}");
        this.messageQueueClientToOtherInstances.Publish(bytes, exchange, routingKey, false);
        return new JObject { ["accepted"] = true, ["transport"] = "mq" };
    }

    /// <summary>
    /// Resolve the MQ exchange + routing key for shutting down one instance
    /// when no direct TCP connection is held. For Service the message is
    /// addressed to ServiceManager (which acts as the relay); for the other
    /// types we use the well-known per-instance routing key.
    /// </summary>
    private (string? Exchange, string? RoutingKey) ResolveShutdownMqRouting(string instanceType, string instanceId)
    {
        // Find the instance to recover its 'Name' (the routing-key component).
        // Snapshot is cheap and avoids holding a lock across the publish.
        var snapshot = this.instanceStatusManager.Snapshot();
        var match = snapshot.FirstOrDefault(s => s.MailBox.Id == instanceId);
        var name = match?.MailBox.Id ?? instanceId;
        _ = name;

        switch (instanceType)
        {
            case "Gate":
                // GenerateHostMessageToGatePackage takes the gate's Name, not its mailbox id.
                // We don't have Name here directly - the broadcast key works as a fallback
                // because every gate filters on its own routing key in the consumer.
                // For a targeted send we'd need a name lookup; in dev the gate names map 1:1 to mailbox ids.
                return (Consts.HostMgrToGateExchangeName, Consts.HostBroadCastMessagePackageToGate);
            case "Server":
                return (Consts.HostMgrToServerExchangeName, Consts.HostBroadCastMessagePackageToServer);
            case "ServiceManager":
            case "Service":
                // For Service we route to ServiceManager (Args[0] = service host id).
                return (Consts.HostMgrToServiceMgrExchangeName, Consts.HostMessagePackageToServiceMgrPackage);
            case "DbManager":
                // DbManager always connects via immediate TCP (no MQ exchange
                // exists from HostMgr -> DbMgr). If the direct-TCP send in the
                // caller did not find a live connection there is nothing else
                // we can do - report unsupported so the caller surfaces the
                // failure to WebManager instead of silently dropping.
                return (null, null);
            default:
                return (null, null);
        }
    }

    /// <summary>
    /// Snapshot every instance the HostManager has registered, grouped by role.
    /// </summary>
    private JObject BuildClusterOverview()
    {
        var byType = this.instanceStatusManager.Snapshot()
            .GroupBy(s => s.InstanceType.ToString())
            .ToDictionary(g => g.Key, g => g.ToList());

        JArray ToArray(string key) => byType.TryGetValue(key, out var list)
            ? new JArray(list.Select(s => new JObject
            {
                ["id"] = s.MailBox.Id,
                ["ip"] = s.MailBox.Ip,
                ["port"] = s.MailBox.Port,
                ["hostNum"] = s.MailBox.HostNum,
                ["status"] = (int)s.Status,
                ["lastHeartBeat"] = s.LastHeartBeat.ToString("O"),
            }))
            : new JArray();

        return new JObject
        {
            ["hostManager"] = new JObject
            {
                ["ip"] = this.Ip,
                ["port"] = this.Port,
                ["hostNum"] = this.HostNum,
                ["desiredServerNum"] = this.DesiredServerNum,
                ["desiredGateNum"] = this.DesiredGateNum,
                ["status"] = this.Status.ToString(),
            },
            ["gates"] = ToArray(InstanceType.Gate.ToString()),
            ["servers"] = ToArray(InstanceType.Server.ToString()),
            ["serviceManagers"] = ToArray(InstanceType.ServiceManager.ToString()),
            ["services"] = ToArray(InstanceType.Service.ToString()),
            ["dbManagers"] = ToArray(InstanceType.DbManager.ToString()),
        };
    }
}
