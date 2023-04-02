namespace LPS.Server.WebManager.Services;

using Common.Debug;
using LPS.Common.Ipc;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;
using ServerInfoData = ValueTuple<int, List<Newtonsoft.Json.Linq.JObject>>;

public class ServerService
{
    private readonly MessageQueueClient client = new MessageQueueClient();
    private readonly AsyncTaskGenerator<JObject> asyncTaskGeneratorForServerCnt = new AsyncTaskGenerator<JObject>();

    private readonly AsyncTaskGenerator<List<JObject>, ServerInfoData>
        asyncTaskGenerateForGetServerInfo = new AsyncTaskGenerator<List<JObject>, ServerInfoData>();

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
    public Task<JObject> GetServerBasicInfo()
    {
        return this.SendMessage(new { }, Consts.GetServerBasicInfoRoutingKey, this.asyncTaskGeneratorForServerCnt);
    }

    /// <summary>
    /// Get server info.
    /// </summary>
    /// <param name="targetServerCnt">Cnt of the server info.</param>
    /// <returns></returns>
    public Task<List<JObject>> GetServerInfo(int targetServerCnt)
    {
        return this.SendMessage(
            new { },
            Consts.CollectServerInfo,
            this.asyncTaskGenerateForGetServerInfo,
            (targetServerCnt, new List<JObject>()));
    }

    private void HandleMqMessage(string msg, string routingKey)
    {
        Logger.Debug($"message received, {msg}, {routingKey}");
        if (routingKey == Consts.ServerBasicInfoResRoutingKey)
        {
            var (rpcId, json) = MessageQueueJsonBody.From(msg);
            this.asyncTaskGeneratorForServerCnt.ResolveAsyncTask(rpcId, json);
        }
        else if (routingKey == Consts.ServerInfo)
        {
            var (rpcId, json) = MessageQueueJsonBody.From(msg);
            var (totalCnt, svrInfoList) = this.asyncTaskGenerateForGetServerInfo.GetDataByAsyncTaskId(rpcId);

            svrInfoList.Add(json);

            --totalCnt;
            if (totalCnt == 0)
            {
                this.asyncTaskGenerateForGetServerInfo.ResolveAsyncTask(
                    rpcId, 
                    svrInfoList);
            }
            else
            {
                this.asyncTaskGenerateForGetServerInfo.UpdateDataByAsyncTaskId(rpcId, (totalCnt, svrInfoList));
            }
        }
    }

    private Task<TResult> SendMessage<TResult>(object body, string routingKey,
        AsyncTaskGenerator<TResult> asyncTaskGenerator)
    {
        var (task, id) = asyncTaskGenerator.GenerateAsyncTask();
        var msg = MessageQueueJsonBody.Create(id, body).ToJson();
        this.client.Publish(msg, Consts.WebMgrExchangeName, routingKey);
        return task;
    }

    private Task<TResult> SendMessage<TResult, TData>(
        object body,
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