// -----------------------------------------------------------------------
// <copyright file="DbApi.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.DbApi;

using LPS.Common.Debug;
using LPS.Server.Database.Storage;
using LPS.Server.Database.Storage.Attribute;
using LPS.Server.Database.Storage.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Provides a set of static methods for interacting with the database.
/// </summary>
[DbApiProvider(typeof(MongoDbWrapper))]
public class DbApi
{
    /// <summary>
    /// Queries the database for an account with the specified username.
    /// </summary>
    /// <param name="database">The database instance to query.</param>
    /// <param name="userName">The username to search for.</param>
    /// <returns>An <see cref="IDbDataSet"/> object representing the account data, or null if no account was found.</returns>
    [DbApi]
    public async Task<(string Password, string AccountId)> QueryAccountByUserName(MongoDbWrapper database, string userName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("username", userName);
        var coll = database.GetCollectionFromDefaultDb("account");
        var res = await coll.FindAsync(filter);
        var resColl = await res.ToListAsync();

        if (!resColl.Any())
        {
            return (string.Empty, string.Empty);
        }
        else
        {
            var account = resColl.First();
            var password = account["password"].AsString;
            var accountId = account["_id"].AsObjectId.ToString();
            return (password, accountId);
        }
    }

    /// <summary>
    /// Creates a new player entity data into Player collection.
    /// </summary>
    /// <param name="database">The database instance to use.</param>
    /// <param name="accountId">The accountId related to player entity.</param>
    /// <returns>A boolean value indicating whether the player account was successfully created.</returns>
    [DbApi]
    public async Task<string> CreatePlayerIfNotExist(MongoDbWrapper database, string accountId)
    {
        var coll = database.GetCollectionFromDefaultDb("player");

        var accountIdAsObjectId = new BsonObjectId(new ObjectId(accountId));
        var filter = Builders<BsonDocument>.Filter.Eq(
            "AccountId", accountIdAsObjectId);
        var query = await coll.FindAsync<BsonDocument>(filter);
        var resColl = await query.ToListAsync();

        if (resColl.Any())
        {
            var playerId = resColl.First()["_id"];
            var playerStrId = playerId.AsObjectId.ToString();
            Logger.Debug($"[CreatePlayerIfNotExist] Player exist, id: {playerStrId}");
            return playerStrId;
        }
        else
        {
            var insertData = new BsonDocument()
            {
                ["AccountId"] = accountIdAsObjectId,
                ["Name"] = "Unkown",
            };
            await coll.InsertOneAsync(insertData);
            var playerStrId = insertData["_id"].AsObjectId.ToString();
            Logger.Debug($"[CreatePlayerIfNotExist] Player not exist, create a new, id: {playerStrId}");
            return playerStrId;
        }
    }
}
