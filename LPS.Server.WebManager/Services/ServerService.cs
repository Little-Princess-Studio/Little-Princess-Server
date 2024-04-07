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
    
    private void HandleMqMessage(string msg, string routingKey)
    {
        Logger.Debug($"message received, {msg}, {routingKey}");
        var (rpcId, json) = MessageQueueJsonBody.From(msg);

        if (routingKey is Consts.ServerBasicInfoRes or Consts.ServerDetailedInfo or Consts.AllEntitiesRes)
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