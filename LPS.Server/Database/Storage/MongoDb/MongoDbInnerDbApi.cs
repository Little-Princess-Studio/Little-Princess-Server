// -----------------------------------------------------------------------
// <copyright file="MongoDbInnerDbApi.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
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
        var collName = args[0].Unpack<StringArg>().PayLoad;
        var objectId = args[1].Unpack<StringArg>().PayLoad;
        var componentName = args[2].Unpack<StringArg>().PayLoad;

        return mongoDb.LoadComponent(collName, objectId, componentName);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public async Task<Any> SaveComponent(MongoDbWrapper database, Any[] args)
    {
        var collName = args[0].Unpack<StringArg>().PayLoad;
        var objectId = args[1].Unpack<StringArg>().PayLoad;
        var componentName = args[2].Unpack<StringArg>().PayLoad;
        var componentData = args[3];

        var compData = await database.SaveComponent(collName, objectId, componentName, componentData: componentData);
        return Any.Pack(new BoolArg { PayLoad = compData });
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> LoadEntity(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = args[0].Unpack<StringArg>().PayLoad;
        var keyName = args[1].Unpack<StringArg>().PayLoad;
        var value = args[2].Unpack<StringArg>().PayLoad;
        return mongoDb.LoadEntity(collName, keyName, value);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> SaveEntity(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = args[0].Unpack<StringArg>().PayLoad;
        var id = args[1].Unpack<StringArg>().PayLoad;
        return mongoDb.SaveEntity(collName, id, args[2])
            .ContinueWith(
                t => Any.Pack(
                    new BoolArg() { PayLoad = t.Result }));
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public Task<Any> BatchLoadComponents(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = args[0].Unpack<StringArg>().PayLoad;
        var objectId = args[1].Unpack<StringArg>().PayLoad;
        var names = args[2].Unpack<ListArg>().PayLoad;
        var componentsNameList = names.Select(name => name.Unpack<StringArg>().PayLoad).ToArray();

        return mongoDb.BatchLoadComponents(collName, objectId, componentsNameList);
    }

    /// <inheritdoc/>
    [DbInnerApi]
    public async Task<Any> BathcSaveComponents(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = args[0].Unpack<StringArg>().PayLoad;
        var objectId = args[1].Unpack<StringArg>().PayLoad;
        var updateDict = args[2].Unpack<DictWithStringKeyArg>().PayLoad;
        var converted = updateDict.ToDictionary(pair => pair.Key, pair => pair.Value);

        var res = await mongoDb.BatchSaveComponents(collName, objectId, converted);
        return Any.Pack(new BoolArg { PayLoad = res });
    }
}
