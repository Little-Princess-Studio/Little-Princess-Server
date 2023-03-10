// -----------------------------------------------------------------------
// <copyright file="IGlobalCache.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core.Database.GlobalCache
{
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for global cache.
    /// </summary>
    /// <typeparam name="T">Type of the operation value.</typeparam>
    public interface IGlobalCache<T>
    {
        /// <summary>
        /// Sets the key-val.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="val">Value.</param>
        /// <returns>Result.</returns>
        Task<bool> Set(string key, T val);

        /// <summary>
        /// Gets the value from key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        Task<T> Get(string key);
    }

    /// <summary>
    /// IGlobalCache interface.
    /// </summary>
    public interface IGlobalCache : IGlobalCache<long>, IGlobalCache<string>
    {
        /// <summary>
        /// Initialize global cache.
        /// </summary>
        /// <returns>Result of the initialization.</returns>
        Task<bool> Initialize();

        /// <summary>
        /// Clear global cache, it's the dangerous operation.
        /// </summary>
        /// <returns>Reuslt of the clear operation.</returns>
        Task Clear();

        /// <summary>
        /// Increase a value by 1.
        /// </summary>
        /// <param name="key">Key name.</param>
        /// <returns>Increase result.</returns>
        Task<long> Incr(string key);

        /// <summary>
        /// Gets the native client object (for example, RedisClient).
        /// GetNativeClient is a dangerous method which return the native db client object to user
        /// user should clearly know what the db client is and do costume operation on it.
        /// </summary>
        /// <returns>Native client object.</returns>
        object GetNativeClient();
    }
}