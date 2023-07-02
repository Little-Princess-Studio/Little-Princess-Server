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
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Server.MessageQueue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Represents a client for interacting with a database.
/// </summary>
public class DbClient
{
    private readonly MessageQueueClient msgQueueClient;
    private readonly string identifier;
    private readonly AsyncTaskGenerator<string?> asyncTaskGenerator = new();

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
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a <see cref="JObject"/>.</returns>
    public async Task<JObject?> InvokeDbApi(string apiName, params object[]? args)
    {
        var (task, id) = this.asyncTaskGenerator.GenerateAsyncTask(
            5000, (id) =>
        {
            this.asyncTaskGenerator.ResolveAsyncTask(id, null);
            return new RpcTimeOutException($"DbApi {apiName} timeout.");
        });

        var jargs = new JArray();

        if (args is not null)
        {
            foreach (var arg in args)
            {
                var item = JToken.FromObject(arg);
                if (item is null)
                {
                    var e = new ArgumentException($"Invalid argument type {arg.GetType()} for DbApi {apiName}.");
                    Logger.Error(e);
                    throw e;
                }

                jargs.Add(JToken.FromObject(arg));
            }
        }

        var msg = new JObject
        {
            ["id"] = id,
            ["apiName"] = apiName,
            ["args"] = jargs,
        }.ToString();

        this.msgQueueClient.Publish(
            Encoding.UTF8.GetBytes(msg),
            Consts.DbClientToDbMgrExchangeName,
            Consts.GenerateDbClientMessagePackageToDbMgr(this.identifier));

        var res = await task;
        return res == null ? null : JObject.Parse(res);
    }

    private void HandleDbMgrMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        if (routingKey != Consts.GenerateDbMgrMessagePackageToDbClient(this.identifier))
        {
            return;
        }

        using var stream = new MemoryStream(msg.ToArray());
        using var reader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(reader);
        JObject json = JObject.Load(jsonReader);

        var id = json["id"]?.ToObject<uint>();

        if (id is null)
        {
            Logger.Warn($"Invalid result recieved, {json}");
            return;
        }

        var res = json["result"]?.ToString();
        if (res is null)
        {
            Logger.Warn($"Invalid result recieved, {json}");
            return;
        }

        this.asyncTaskGenerator.ResolveAsyncTask((uint)id, res);
    }
}