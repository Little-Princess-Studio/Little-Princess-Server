namespace LPS.Server.WebManager.Services;

using Common.Debug;
using LPS.Common.Ipc;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;

/// <summary>
/// WebManager <-> cluster RPC client. Every round-trip is described by a
/// <see cref="WebMgrEndpoints.Endpoint"/>; the dispatch table on the cluster
/// side knows the matching reply key, and this side filters inbound messages
/// via <see cref="WebMgrEndpoints.ReplyKeys"/>. No hand-maintained
/// routing-key disjunctions.
/// </summary>
public class ServerService
{
    private readonly MessageQueueClient client = new();
    private readonly AsyncTaskGenerator<JToken> asyncTaskGeneratorForJObjectRes = new();

    /// <summary>Connect to the broker and start consuming WebManager replies.</summary>
    public void Init()
    {
        this.client.Init();
        this.client.AsProducer();
        this.client.AsConsumer();
        this.client.DeclareExchange(Consts.WebMgrExchangeName);
        this.client.DeclareExchange(Consts.ServerExchangeName);
        this.client.BindQueueAndExchange(
            Consts.WebManagerQueueName,
            Consts.ServerExchangeName,
            Consts.RoutingKeyToWebManager);
        this.client.Observe(Consts.WebManagerQueueName, this.HandleMqMessage);
    }

    /// <summary>HostManager -> server count + mailboxes.</summary>
    public Task<JToken> GetServerBasicInfo() => this.Call(WebMgrEndpoints.ServerBasicInfo, new JObject());

    /// <summary>Server-specific detailed info.</summary>
    public Task<JToken> GetServerDetailedInfo(string serverId, int hostNum)
        => this.Call(WebMgrEndpoints.ServerDetailedInfo, new JObject { ["serverId"] = serverId, ["hostNum"] = hostNum });

    /// <summary>Every entity/cell living on one server.</summary>
    public Task<JToken> GetAllEntitiesOfServer(string serverId, int hostNum)
        => this.Call(WebMgrEndpoints.AllEntitiesOfServer, new JObject { ["serverId"] = serverId, ["hostNum"] = hostNum });

    /// <summary>Per-server ping/pong status from HostManager.</summary>
    public Task<JToken> GetAllServerPingPongInfo() => this.Call(WebMgrEndpoints.ServerPingPong, new JObject());

    /// <summary>HostManager-level cluster overview (gates/servers/svcmgrs/services).</summary>
    public Task<JToken> GetClusterOverview() => this.Call(WebMgrEndpoints.ClusterOverview, new JObject());

    /// <summary>ServiceManager-level service shard roster.</summary>
    public Task<JToken> GetServicesRoster() => this.Call(WebMgrEndpoints.ServicesList, new JObject());

    /// <summary>One gate's detailed runtime state.</summary>
    public Task<JToken> GetGateDetailedInfo(string gateId, int hostNum)
        => this.Call(WebMgrEndpoints.GateDetailedInfo, new JObject { ["gateId"] = gateId, ["hostNum"] = hostNum });

    /// <summary>One service shard's detailed runtime state.</summary>
    public Task<JToken> GetServiceShardDetailedInfo(string serviceName, uint shard)
        => this.Call(WebMgrEndpoints.ServiceShardDetailedInfo, new JObject { ["serviceName"] = serviceName, ["shard"] = shard });

    private Task<JToken> Call(WebMgrEndpoints.Endpoint endpoint, JToken body)
    {
        var (task, id) = this.asyncTaskGeneratorForJObjectRes.GenerateAsyncTask();
        var msg = MessageQueueJsonBody.Create(id, body).ToJson();
        Logger.Debug($"[WebMgr->] {endpoint.Request} (msgId={id})");
        this.client.Publish(msg, Consts.WebMgrExchangeName, endpoint.Request);
        return task;
    }

    private void HandleMqMessage(string msg, string routingKey)
    {
        // WebMgrEndpoints.ReplyKeys is the single source of truth for "is
        // this an answer to one of our calls?" - no more hand-rolled
        // routing-key disjunctions to keep in sync.
        if (!WebMgrEndpoints.ReplyKeys.Contains(routingKey))
        {
            Logger.Debug($"[WebMgr<-] unknown reply routingKey={routingKey} (ignored)");
            return;
        }

        var (rpcId, json) = MessageQueueJsonBody.From(msg);
        Logger.Debug($"[WebMgr<-] {routingKey} (msgId={rpcId})");
        this.asyncTaskGeneratorForJObjectRes.ResolveAsyncTask(rpcId, json);
    }
}
