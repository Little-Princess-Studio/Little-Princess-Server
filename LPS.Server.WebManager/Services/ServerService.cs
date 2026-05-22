namespace LPS.Server.WebManager.Services;

using Common.Debug;
using LPS.Common.Ipc;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;
using ServerInfoData = ValueTuple<int, List<Newtonsoft.Json.Linq.JToken>>;

public class ServerService
{
    private readonly MessageQueueClient client = new MessageQueueClient();
    private readonly AsyncTaskGenerator<JToken> asyncTaskGeneratorForJObjectRes = new AsyncTaskGenerator<JToken>();

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

    /// <summary>
    /// Get server cnt from server host manager.
    /// </summary>
    /// <returns>Server cnt.</returns>
    public Task<JToken> GetServerBasicInfo()
    {
        return this.SendMessageWithReplay(new JObject(), Consts.GetServerBasicInfo, this.asyncTaskGeneratorForJObjectRes);
    }


    /// <summary>
    /// Get detailed info of a server.
    /// </summary>
    /// <param name="serverId">Id of the server.</param>
    /// <param name="hostNum">Hostnum of the server</param>
    /// <returns></returns>
    public Task<JToken> GetServerDetailedInfo(string serverId, int hostNum)
    {
        return this.SendMessageWithReplay(
            new JObject
            {
                ["serverId"] = serverId,
                ["hostNum"] = hostNum,
            },
            Consts.GetServerDetailedInfo,
            this.asyncTaskGeneratorForJObjectRes);
    }

    public Task<JToken> GetAllEntitiesOfServer(string serverId, int hostNum)
    {
        return this.SendMessageWithReplay(
            new JObject
            {
                ["serverId"] = serverId,
                ["hostNum"] = hostNum,
            },
            Consts.GetAllEntitiesOfServer,
            this.asyncTaskGeneratorForJObjectRes);
    }

    public Task<JToken> GetAllServerPingPongInfo()
    {
        return this.SendMessageWithReplay(
            new JObject(),
            Consts.GetServerPingPongInfo,
            this.asyncTaskGeneratorForJObjectRes);
    }

    /// <summary>
    /// Single-shot snapshot of every instance the HostManager has registered,
    /// suitable for rendering the cluster overview page.
    /// </summary>
    /// <returns>HostManager + gates + servers + serviceManagers + services.</returns>
    public Task<JToken> GetClusterOverview()
    {
        return this.SendMessageWithReplay(
            new JObject(),
            Consts.GetClusterOverview,
            this.asyncTaskGeneratorForJObjectRes);
    }

    /// <summary>
    /// Single-shot snapshot of the ServiceManager routing map (every service
    /// name -> shards -> mailbox). Complements <see cref="GetClusterOverview"/>
    /// which only sees what HostManager directly registers.
    /// </summary>
    /// <returns>serviceManager + services[] with shard mailboxes.</returns>
    public Task<JToken> GetServicesRoster()
    {
        return this.SendMessageWithReplay(
            new JObject(),
            Consts.GetServiceList,
            this.asyncTaskGeneratorForJObjectRes);
    }

    /// <summary>
    /// Live runtime state of one Gate instance (connections, bound client
    /// entities, queue depth, etc). The request is broadcast on
    /// <see cref="Consts.RoutingKeyWebManagerToGate"/>; only the gate whose
    /// id+hostNum match replies.
    /// </summary>
    /// <param name="gateId">Mailbox id of the gate (raw, not URL-encoded).</param>
    /// <param name="hostNum">Host number of the gate.</param>
    /// <returns>Gate detailed info JSON.</returns>
    public Task<JToken> GetGateDetailedInfo(string gateId, int hostNum)
    {
        return this.SendMessageWithReplay(
            new JObject
            {
                ["gateId"] = gateId,
                ["hostNum"] = hostNum,
            },
            Consts.GetGateDetailedInfo,
            this.asyncTaskGeneratorForJObjectRes);
    }

    /// <summary>
    /// Live runtime state of one service shard (identified by service name +
    /// shard index). Only the Service host process that owns the shard
    /// replies.
    /// </summary>
    /// <param name="serviceName">Class name of the service (e.g. EchoService).</param>
    /// <param name="shard">Shard index inside the service.</param>
    /// <returns>Service shard detailed info JSON.</returns>
    public Task<JToken> GetServiceShardDetailedInfo(string serviceName, uint shard)
    {
        return this.SendMessageWithReplay(
            new JObject
            {
                ["serviceName"] = serviceName,
                ["shard"] = shard,
            },
            Consts.GetServiceShardDetailedInfo,
            this.asyncTaskGeneratorForJObjectRes);
    }
    
    private void HandleMqMessage(string msg, string routingKey)
    {
        Logger.Debug($"message received, {msg}, {routingKey}");
        var (rpcId, json) = MessageQueueJsonBody.From(msg);

        if (routingKey is Consts.ServerBasicInfoRes
            or Consts.ServerDetailedInfo
            or Consts.AllEntitiesRes
            or Consts.GetServerPingPongInfoRes
            or Consts.GetClusterOverviewRes
            or Consts.GetServiceListRes
            or Consts.GateDetailedInfoRes
            or Consts.ServiceShardDetailedInfoRes)
        {
            this.asyncTaskGeneratorForJObjectRes.ResolveAsyncTask(rpcId, json);
        }
    }

    private Task<TResult> SendMessageWithReplay<TResult>(JToken body, string routingKey,
        AsyncTaskGenerator<TResult> asyncTaskGenerator)
    {
        var (task, id) = asyncTaskGenerator.GenerateAsyncTask();
        var msg = MessageQueueJsonBody.Create(id, body).ToJson();
        this.client.Publish(msg, Consts.WebMgrExchangeName, routingKey);
        return task;
    }

    private Task<TResult> SendMessageWithReplay<TResult, TData>(
        JObject body,
        string routingKey,
        AsyncTaskGenerator<TResult, TData> asyncTaskGenerator,
        TData data)
    {
        var (task, id) = asyncTaskGenerator.GenerateAsyncTask(data);
        var msg = MessageQueueJsonBody.Create(id, body).ToJson();
        this.client.Publish(msg, Consts.WebMgrExchangeName, routingKey);
        return task;
    }
}