// -----------------------------------------------------------------------
// <copyright file="DbClient.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Represents a client for interacting with a database.
/// </summary>
public class DbClient
{
    private readonly MessageQueueClient msgQueueClient;
    private readonly string identifier;
    private readonly AsyncTaskGenerator<object?, Type> asyncTaskGenerator = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbClient"/> class.
    /// </summary>
    /// <param name="identifier">The identifier for the database client.</param>
    public DbClient(string identifier)
    {
        this.msgQueueClient = new MessageQueueClient();
        this.identifier = identifier;
    }

    /// <summary>
    /// Initializes the database client.
    /// </summary>
    public void Initialize()
    {
        Logger.Debug("Start mq client for db manager.");
        this.msgQueueClient.Init();
        this.msgQueueClient.AsProducer();
        this.msgQueueClient.AsConsumer();

        this.msgQueueClient.DeclareExchange(Consts.DbMgrToDbClientExchangeName);
        this.msgQueueClient.DeclareExchange(Consts.DbClientToDbMgrExchangeName);

        this.msgQueueClient.BindQueueAndExchange(
            Consts.GenerateDbClientQueueName(this.identifier),
            Consts.DbMgrToDbClientExchangeName,
            Consts.GenerateDbMgrMessagePackageToDbClient(this.identifier));

        this.msgQueueClient.Observe(
            Consts.GenerateDbClientQueueName(this.identifier),
            this.HandleDbMgrMqMessage);
    }

    /// <summary>
    /// Invokes a database API with the specified name and arguments.
    /// </summary>
    /// <param name="apiName">The name of the API to invoke.</param>
    /// <param name="args">The arguments to pass to the API. Every arg should be able to serialized by JToken. </param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a <see cref="JObject"/>.</returns>
    public async Task<T> CallDbApi<T>(string apiName, params object[]? args)
    {
        var (task, id) = this.asyncTaskGenerator.GenerateAsyncTask(
            typeof(T), 5000, (id) =>
        {
            this.asyncTaskGenerator.ResolveAsyncTask(id, null);
            return new RpcTimeOutException($"DbApi {apiName} timeout.");
        });

        var dbrpc = new DatabaseManagerRpc()
        {
            ApiName = apiName,
            RpcId = id,
        };

        if (args is not null)
        {
            foreach (var arg in args)
            {
                dbrpc.Args.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcHelper.RpcArgToProtoBuf(arg)));
            }
        }

        this.msgQueueClient.Publish(
            dbrpc.ToByteArray(),
            Consts.DbClientToDbMgrExchangeName,
            Consts.GenerateDbClientMessagePackageToDbMgr(this.identifier));

        var res = await task;
        return (T)res!;
    }

    private void HandleDbMgrMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        if (routingKey != Consts.GenerateDbMgrMessagePackageToDbClient(this.identifier))
        {
            return;
        }

        try
        {
            var res = new MessageParser<DatabaseManagerRpcRes>(() => new DatabaseManagerRpcRes());
            var databaseRpcRes = res.ParseFrom(msg.ToArray());

            var id = databaseRpcRes.RpcId;

            if (this.asyncTaskGenerator.ContainsAsyncId(id))
            {
                var returnType = this.asyncTaskGenerator.GetDataByAsyncTaskId(id);
                var result = databaseRpcRes.Res;

                var parsed = RpcHelper.ProtoBufAnyToRpcArg(result, returnType);
                this.asyncTaskGenerator.ResolveAsyncTask((uint)id, parsed);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HandleDbMgrMqMessage error.");
        }
    }
}