// -----------------------------------------------------------------------
// <copyright file="MongoDbWrapper.cs" company="Little Princess Studio">
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
public class MongoDbWrapper : IDatabase
{
    /// <summary>
    /// Gets the <see cref="MongoClient"/> instance used by this <see cref="MongoDbWrapper"/> instance.
    /// </summary>
    public MongoClient Client { get; private set; } = null!;

    /// <inheritdoc/>
    public Task<bool> Initialize(string connectString)
    {
        this.Client = new MongoClient(connectString);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets the specified collection from the specified database.
    /// </summary>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="tableName">The name of the collection.</param>
    /// <returns>The <see cref="IMongoCollection{TDocument}"/> instance representing the specified collection.</returns>
    public IMongoCollection<BsonDocument> GetCollection(string databaseName, string tableName)
    {
        return this.Client
                .GetDatabase(databaseName)
                .GetCollection<BsonDocument>(tableName);
    }

    /// <inheritdoc/>
    public Task<bool> ShutDown()
    {
        return Task.FromResult(true);
    }
}
