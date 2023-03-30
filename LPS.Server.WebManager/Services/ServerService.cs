namespace LPS.Server.WebManager.Services;

using Common.Debug;
using LPS.Common.Ipc;
using LPS.Server.MessageQueue;

public class ServerService
{
    private readonly MessageQueueClient client = new MessageQueueClient();
    private readonly AsyncTaskGenerator<int> asyncTaskGeneratorForServerCnt = new AsyncTaskGenerator<int>();

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
        this.client.Observe(Consts.WebManagerQueueName, this.HandleMessage);
    }

    /// <summary>
    /// Get server cnt from server host manager.
    /// </summary>
    /// <returns>Server cnt.</returns>
    public Task<int> GetServerCnt()
    {
        return this.SendMessage(new { }, Consts.GetServerCntRoutingKey, this.asyncTaskGeneratorForServerCnt);
    }

    private void HandleMessage(string msg, string routingKey)
    {
        Logger.Debug($"message received, {msg}, {routingKey}");
        if (routingKey == Consts.ServerCntResRoutingKey)
        {
            var (rpcId, json) = MessageQueueJsonBody.From(msg);
            var cnt = json["cnt"] !.ToObject<int>();
            this.asyncTaskGeneratorForServerCnt.ResolveAsyncTask(rpcId, cnt);
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
}