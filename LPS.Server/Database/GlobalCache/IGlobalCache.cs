// -----------------------------------------------------------------------
// <copyright file="IGlobalCache.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.GlobalCache;

using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// IGlobalCache interface.
/// </summary>
public interface IGlobalCache
{
    /// <summary>
    /// Initialize global cache.
    /// </summary>
    /// <returns>Result of the initialization.</returns>
    /// <summary>
    /// Initializes the global cache with the specified initialization string.
    /// </summary>
    /// <param name="initString">The initialization string.</param>
    /// <returns>True if the initialization was successful, otherwise false.</returns>
    Task<bool> Initialize(string initString);

    /// <summary>
    /// Clear global cache, it's the dangerous operation.
    /// </summary>
    /// <returns>Reuslt of the clear operation.</returns>
    Task Clear();

    /// <summary>
    /// Generate a runtime global-unique id in global cache.
    /// </summary>
    /// <returns>Global-unique id string.</returns>
    Task<long> GenerateNewGlobalId();

    /// <summary>
    /// Gets the native client object (for example, RedisClient).
    /// GetNativeClient is a dangerous method which return the native db client object to user
    /// user should clearly know what the db client is and do costume operation on it.
    /// </summary>
    /// <typeparam name="T">The type of the native client object.</typeparam>
    /// <returns>The native client object.</returns>
    T? GetNativeClient<T>()
        where T : class;
}