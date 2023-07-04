// -----------------------------------------------------------------------
// <copyright file="MongoDbInnerDbApi.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Database.Storage.Attribute;

/// <summary>
/// Provides inner database API for MongoDB.
/// </summary>
[DbInnerApiProvider(typeof(MongoDbWrapper))]
public class MongoDbInnerDbApi : IDatabaseInnerApi<MongoDbWrapper>
{
    /// <summary>
    /// Loads an entity from the specified MongoDB collection based on the given key-value pair.
    /// </summary>
    /// <param name="mongoDb">The MongoDB wrapper instance.</param>
    /// <param name="args">The arguments for the method call.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded entity.</returns>
    public static Task<Any> LoadEntity(MongoDbWrapper mongoDb, Any[] args)
    {
        var collName = args[1].Unpack<StringArg>().PayLoad;
        var keyName = args[0].Unpack<StringArg>().PayLoad;
        var value = args[2].Unpack<StringArg>().PayLoad;
        return mongoDb.LoadEntity(collName, keyName, value);
    }
}
