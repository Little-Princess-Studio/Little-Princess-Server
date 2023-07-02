// -----------------------------------------------------------------------
// <copyright file="MongoDb.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Represents a MongoDB database implementation of the <see cref="IDatabase"/> interface.
/// </summary>
public class MongoDb : IDatabase
{
    private MongoClient client = null!;

    /// <inheritdoc/>
    public Task<bool> Initialize(string connectString)
    {
        this.client = new MongoClient(connectString);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<IDbDataSet?> QueryOne(string databaseName, string tableName, string queryString)
    {
        IMongoCollection<BsonDocument> coll = this.GetCollection(databaseName, tableName);

        IAsyncCursor<BsonDocument>? res = await coll.FindAsync<BsonDocument>(queryString);

        if (res != null)
        {
            return new MongoDbDataSet(await res.FirstAsync());
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<List<IDbDataSet>?> QueryMulti(string databaseName, string tableName, string queryString)
    {
        IMongoCollection<BsonDocument> coll = this.GetCollection(databaseName, tableName);

        IAsyncCursor<BsonDocument>? res = await coll.FindAsync<BsonDocument>(queryString);

        if (res != null)
        {
            return (await res.ToListAsync())
                    .Select(x => new MongoDbDataSet(x) as IDbDataSet)
                    .ToList();
        }

        return null;
    }

    /// <inheritdoc/>
    public object GetRawDbClient()
    {
        return this.client;
    }

    private IMongoCollection<BsonDocument> GetCollection(string databaseName, string tableName)
    {
        return this.client
                .GetDatabase(databaseName)
                .GetCollection<BsonDocument>(tableName);
    }
}
