// -----------------------------------------------------------------------
// <copyright file="MongoDbWrapper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Represents a MongoDB database implementation of the <see cref="IDatabase"/> interface.
/// </summary>
public class MongoDbWrapper : IDatabase
{
    /// <summary>
    /// Gets the <see cref="MongoClient"/> instance used by this <see cref="MongoDbWrapper"/> instance.
    /// </summary>
    public MongoClient Client { get; private set; } = null!;

    /// <inheritdoc/>
    public string DefaultDatabaseName => this.defaultDatabaseName;

    private readonly string defaultDatabaseName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbWrapper"/> class.
    /// </summary>
    /// <param name="defaultDatabaseName">The default database name.</param>
    public MongoDbWrapper(string defaultDatabaseName)
    {
        this.defaultDatabaseName = defaultDatabaseName;
    }

    /// <inheritdoc/>
    public Task<bool> Initialize(string connectString)
    {
        this.Client = new MongoClient(connectString);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> ShutDown()
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<Any> LoadEntity(string collectionName, string keyName, string value)
    {
        var coll = this.GetCollection(this.defaultDatabaseName, collectionName);
        var filter = Builders<BsonDocument>.Filter.Eq(keyName, value);
        var queryRes = (await coll.FindAsync(filter)).ToList();

        if (queryRes.Any())
        {
            var entity = queryRes.First();

            // todo: serialize bson to any
        }

        return new Any();
    }

    /// <summary>
    /// Gets the <see cref="IMongoCollection{TDocument}"/> instance for the specified database and collection names.
    /// </summary>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <returns>The <see cref="IMongoCollection{TDocument}"/> instance for the specified database and collection names.</returns>
    public IMongoCollection<BsonDocument> GetCollection(string databaseName, string collectionName)
    {
        return this.Client
                .GetDatabase(databaseName)
                .GetCollection<BsonDocument>(collectionName);
    }

    /// <summary>
    /// Gets the <see cref="IMongoCollection{TDocument}"/> instance for the specified collection name in the default database.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <returns>The <see cref="IMongoCollection{TDocument}"/> instance for the specified collection name in the default database.</returns>
    public IMongoCollection<BsonDocument> GetCollectionFromDefaultDb(string collectionName)
    {
        return this.Client
                .GetDatabase(this.defaultDatabaseName)
                .GetCollection<BsonDocument>(collectionName);
    }
}
