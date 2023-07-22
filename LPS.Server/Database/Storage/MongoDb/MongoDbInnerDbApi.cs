// -----------------------------------------------------------------------
// <copyright file="MongoDbInnerDbApi.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Database.Storage.Attribute;

/// <summary>
/// Provides inner database API for MongoDB.
/// </summary>
[DbInnerApiProvider(typeof(MongoDbWrapper))]
public class MongoDbInnerDbApi : IDbInnerApi<MongoDbWrapper>
{
    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> LoadComponent(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = RpcHelper.GetString(args[0]);
        var objectId = RpcHelper.GetString(args[1]);
        var componentName = RpcHelper.GetString(args[2]);

        return mongoDb.LoadComponent(collName, objectId, componentName);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public async Task<Any> SaveComponent(MongoDbWrapper database, Any[] args)
    {
        var collName = RpcHelper.GetString(args[0]);
        var objectId = RpcHelper.GetString(args[1]);
        var componentName = RpcHelper.GetString(args[2]);
        var componentData = args[3];

        var compData = await database.SaveComponent(collName, objectId, componentName, componentData: componentData);
        return RpcHelper.GetRpcAny(compData);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> LoadEntity(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = RpcHelper.GetString(args[0]);
        var keyName = RpcHelper.GetString(args[1]);
        var value = RpcHelper.GetString(args[2]);
        return mongoDb.LoadEntity(collName, keyName, value);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> SaveEntity(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = RpcHelper.GetString(args[0]);
        var id = RpcHelper.GetString(args[1]);
        return mongoDb.SaveEntity(collName, id, args[2])
            .ContinueWith(
                t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception);
                        return RpcHelper.GetRpcAny(value: false);
                    }

                    return RpcHelper.GetRpcAny(value: t.Result);
                });
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> BatchLoadComponents(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = RpcHelper.GetString(args[0]);
        var objectId = RpcHelper.GetString(args[1]);
        var names = args[2].Unpack<ListArg>().PayLoad;
        var componentsNameList = names.Select(name => RpcHelper.GetString(name)).ToArray();

        return mongoDb.BatchLoadComponents(collName, objectId, componentsNameList);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public async Task<Any> BathcSaveComponents(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = RpcHelper.GetString(args[0]);
        var objectId = RpcHelper.GetString(args[1]);
        var updateDict = args[2].Unpack<DictWithStringKeyArg>().PayLoad;
        var converted = updateDict.ToDictionary(pair => pair.Key, pair => pair.Value);

        var res = await mongoDb.BatchSaveComponents(collName, objectId, converted);
        return RpcHelper.GetRpcAny(res);
    }
}
