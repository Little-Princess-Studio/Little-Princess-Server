// -----------------------------------------------------------------------
// <copyright file="Redis.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.GlobalCache;

using System;
using System.Threading.Tasks;
using CSRedis;
using LPS.Common.Debug;

/// <summary>
/// Redis implementation for global cache.
/// </summary>
public class Redis : IGlobalCache
{
    /// <summary>
    /// Gets the instance of the redis client.
    /// </summary>
    public static readonly Redis Instance = new();

    /// <inheritdoc/>
    public Task<bool> Initialize(string initString)
    {
        // string connectString = $"{ip}:{port},password={password},defaultDatabase={defaultDatabase}";
        var connectString = initString;

        Logger.Info($"Connecting to redis with {connectString}");

        try
        {
            RedisHelper.Initialization(new CSRedisClient(connectString));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error when connecting to redis");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task Clear() => RedisHelper.Instance.ScriptFlushAsync();

    /// <inheritdoc/>
    public T? GetNativeClient<T>()
        where T : class => RedisHelper.Instance as T;

    /// <inheritdoc/>
    public Task<long> GenerateNewGlobalId()
    {
        const string key = "$_lps_sys_entity_id_counter";
        return RedisHelper.IncrByAsync(key);
    }
}