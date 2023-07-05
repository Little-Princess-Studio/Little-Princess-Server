// -----------------------------------------------------------------------
// <copyright file="IDbInnerApi.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage;

using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

/// <summary>
/// Represents an interface for providing inner database API for a specific database type.
/// </summary>
/// <typeparam name="T">The type of the database.</typeparam>
public interface IDbInnerApi<T>
    where T : IDatabase
{
    /// <summary>
    /// Loads an entity from the specified MongoDB collection based on the given key-value pair.
    /// </summary>
    /// <param name="database">The database wrapper instance.</param>
    /// <param name="args">The arguments for the method call.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded entity.</returns>
    Task<Any> LoadEntity(T database, Any[] args);

    /// <summary>
    /// Saves an entity to the specified MongoDB collection.
    /// </summary>
    /// <param name="database">The database wrapper instance.</param>
    /// <param name="args">The arguments for the method call.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the saved entity.</returns>
    Task<Any> SaveEntity(T database, Any[] args);
}