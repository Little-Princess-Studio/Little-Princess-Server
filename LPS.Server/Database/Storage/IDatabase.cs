// -----------------------------------------------------------------------
// <copyright file="IDatabase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage;

using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;

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

    /// <summary>
    /// Loads a component data from the specified entity in the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection to load the component from.</param>
    /// <param name="entityDbId">The unique identifier of the entity in the database.</param>
    /// <param name="componentName">The name of the component to load.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the loaded component.</returns>
    Task<Any> LoadComponent(string collectionName, string entityDbId, string componentName);

    /// <summary>
    /// Saves the component data to the specified entity in the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection to save the component to.</param>
    /// <param name="entityDbId">The unique identifier of the entity in the database.</param>
    /// <param name="componentName">The name of the component to save.</param>
    /// <param name="componentData">The data of the component to save.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task<bool> SaveComponent(string collectionName, string entityDbId, string componentName, Any componentData);

    /// <summary>
    /// Loads multiple component data from the specified entity in the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection to load the components from.</param>
    /// <param name="entityDbId">The unique identifier of the entity in the database.</param>
    /// <param name="componentNames">The names of the components to load.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains an array of loaded components.</returns>
    Task<Any> BatchLoadComponents(string collectionName, string entityDbId, string[] componentNames);

    /// <summary>
    /// Saves multiple component data to the specified entity in the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection to save the components to.</param>
    /// <param name="entityDbId">The unique identifier of the entity in the database.</param>
    /// <param name="componentsDict">A dictionary containing the component names and their corresponding data to save.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task<bool> BatchSaveComponents(string collectionName, string entityDbId, Dictionary<string, Any> componentsDict);
}