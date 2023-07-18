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
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static LPS.Common.Rpc.InnerMessages.PackageHelper;

/// <summary>
/// Represents a client for interacting with a database.
/// </summary>
public class DbClient
{
    private readonly MessageQueueClient msgQueueClient;
    private readonly string identifier;
    private readonly AsyncTaskGenerator<object?, System.Type> asyncTaskGeneratorForDbApi = new();
    private readonly AsyncTaskGenerator<Any> asyncTaskGeneratorForDbInnerApi = new();

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

        this.msgQueueClient.BindQueueAndExchange(
            Consts.GenerateDbClientQueueName(this.identifier),
            Consts.DbMgrToDbClientExchangeName,
            Consts.GenerateDbMgrMessageInnerPackageToDbClient(this.identifier));

        this.msgQueueClient.Observe(
            Consts.GenerateDbClientQueueName(this.identifier),
            this.HandleDbMgrMqMessage);
    }

    /// <summary>
    /// Invokes a database API with the specified name and arguments.
    /// </summary>
    /// <typeparam name="T">The type of the expected result from the API.</typeparam>
    /// <param name="apiName">The name of the API to invoke.</param>
    /// <param name="args">The arguments to pass to the API. Each argument should be serializable by Google.Protobuf.Any.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as an object of type T.</returns>
    public async Task<T> CallDbApi<T>(string apiName, params object[]? args)
    {
        var (task, id) = this.asyncTaskGeneratorForDbApi.GenerateAsyncTask(
            typeof(T), 5000, (id) =>
        {
            this.asyncTaskGeneratorForDbApi.ResolveAsyncTask(id, null);
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
                dbrpc.Args.Add(Any.Pack(RpcHelper.RpcArgToProtoBuf(arg)));
            }
        }

        this.msgQueueClient.Publish(
            dbrpc.ToByteArray(),
            Consts.DbClientToDbMgrExchangeName,
            Consts.GenerateDbClientMessagePackageToDbMgr(this.identifier));

        var res = await task;
        return (T)res!;
    }

    /// <summary>
    /// Invokes an inner database API with the specified name and arguments.
    /// </summary>
    /// <param name="innerApiName">The name of the inner API to invoke.</param>
    /// <param name="args">The arguments to pass to the inner API. Every arg should be able to serialized by Google.Protobuf.Any.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a <see cref="Google.Protobuf.WellKnownTypes.Any"/>.</returns>
    public async Task<Any> CallDbInnerApi(string innerApiName, Any[] args)
    {
        var (task, id) = this.asyncTaskGeneratorForDbInnerApi.GenerateAsyncTask(5000, (id) =>
        {
            this.asyncTaskGeneratorForDbApi.ResolveAsyncTask(asyncId: id, null);
            return new RpcTimeOutException($"DbInnerApi {innerApiName} timeout.");
        });

        var dbrpc = new DatabaseManagerInnerRpc()
        {
            InnerApiName = innerApiName,
            RpcId = id,
        };
        dbrpc.Args.Add(args);

        this.msgQueueClient.Publish(
            dbrpc.ToByteArray(),
            Consts.DbClientToDbMgrExchangeName,
            Consts.GenerateDbClientInnerMessagePackageToDbMgr(this.identifier));

        var res = await task;
        return res;
    }

    private void HandleDbMgrMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        if (routingKey == Consts.GenerateDbMgrMessagePackageToDbClient(this.identifier))
        {
            this.HandleDbApiRes(msg);
        }
        else if (routingKey == Consts.GenerateDbMgrMessageInnerPackageToDbClient(this.identifier))
        {
            this.HandleDbInnerApiRes(msg);
        }
    }

    private void HandleDbApiRes(ReadOnlyMemory<byte> msg)
    {
        try
        {
            var res = MessageParserWrapper<DatabaseManagerRpcRes>.Get();
            var databaseRpcRes = res.ParseFrom(msg.ToArray());

            var id = databaseRpcRes.RpcId;

            if (this.asyncTaskGeneratorForDbApi.ContainsAsyncId(id))
            {
                var returnType = this.asyncTaskGeneratorForDbApi.GetDataByAsyncTaskId(id);
                var result = databaseRpcRes.Res;

                var parsed = RpcHelper.ProtoBufAnyToRpcArg(result, returnType);
                this.asyncTaskGeneratorForDbApi.ResolveAsyncTask((uint)id, parsed);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HandleDbMgrMqMessage error.");
        }
    }

    private void HandleDbInnerApiRes(ReadOnlyMemory<byte> msg)
    {
        try
        {
            var res = MessageParserWrapper<DatabaseManagerInnerRpcRes>.Get();
            var databaseRpcRes = res.ParseFrom(msg.ToArray());

            var id = databaseRpcRes.RpcId;

            if (this.asyncTaskGeneratorForDbInnerApi.ContainsAsyncId(id))
            {
                var result = databaseRpcRes.Res;
                this.asyncTaskGeneratorForDbInnerApi.ResolveAsyncTask((uint)id, result);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HandleDbMgrMqMessage error.");
        }
    }
}