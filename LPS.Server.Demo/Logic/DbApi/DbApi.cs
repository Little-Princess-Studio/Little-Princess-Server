// -----------------------------------------------------------------------
// <copyright file="DbApi.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.DbApi;

using LPS.Server.Database.Storage;
using LPS.Server.Database.Storage.Attribute;
using LPS.Server.Database.Storage.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Provides a set of static methods for interacting with the database.
/// </summary>
[DbApiProvider(typeof(MongoDbWrapper))]
public static class DbApi
{
    /// <summary>
    /// Queries the database for an account with the specified username.
    /// </summary>
    /// <param name="database">The database instance to query.</param>
    /// <param name="userName">The username to search for.</param>
    /// <returns>An <see cref="IDbDataSet"/> object representing the account data, or null if no account was found.</returns>
    [DbApi]
    public static async Task<string> QueryAccountByUserName(MongoDbWrapper database, string userName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("username", userName);
        var coll = database.GetCollection("demo", "account");
        var res = await coll.FindAsync(filter);
        var resColl = await res.ToListAsync();

        if (!resColl.Any())
        {
            return string.Empty;
        }
        else
        {
            var password = resColl.First()["password"].AsString;
            return password;
        }
    }
}
