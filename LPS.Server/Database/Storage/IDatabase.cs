// -----------------------------------------------------------------------
// <copyright file="IDatabase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage;

using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using MongoDB.Driver;

/// <summary>
/// Database interface.
/// </summary>
public interface IDatabase
{
    /// <summary>
    /// Gets the name of the default database.
    /// </summary>
    string DefaultDatabaseName { get; }

    /// <summary>
    /// Initializes the database with the specified connection string.
    /// </summary>
    /// <param name="connectString">The connection string to use to connect to database.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task<bool> Initialize(string connectString);

    /// <summary>
    /// Shuts down the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous shutdown operation.</returns>
    Task<bool> ShutDown();

    /// <summary>
    /// Loads an entity from the specified collection with the specified key-value pair.
    /// </summary>
    /// <param name="collectionName">The name of the collection to load the entity from.</param>
    /// <param name="keyName">The name of the key to search for.</param>
    /// <param name="value">The value of the key to search for.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the loaded entity.</returns>
    Task<Any> LoadEntity(string collectionName, string keyName, string value);

    /// <summary>
    /// Saves an entity to the specified collection with the specified key-value pair.
    /// </summary>
    /// <param name="collectionName">The name of the collection to save the entity to.</param>
    /// <param name="entityDbId">The unique identifier of the entity in the database.</param>
    /// <param name="entityValue">The value of the entity to save.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task<bool> SaveEntity(string collectionName, string entityDbId, Any entityValue);
}