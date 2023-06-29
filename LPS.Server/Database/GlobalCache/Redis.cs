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
    public Task Clear() => RedisHelper.ScriptFlushAsync();

    /// <inheritdoc/>
    public object GetNativeClient() => RedisHelper.Instance;

    /// <inheritdoc/>
    public Task<long> Incr(string key) => RedisHelper.IncrByAsync(key);

    /// <inheritdoc/>
    public Task<bool> Set(string key, long val) => RedisHelper.SetAsync(key, val);

    /// <inheritdoc/>
    public Task<long> Get(string key) => RedisHelper.GetAsync<long>(key);

    /// <inheritdoc/>
    public Task<bool> Set(string key, string val) => RedisHelper.SetAsync(key, val);

    /// <inheritdoc/>
    Task<string> IGlobalCache<string>.Get(string key) => RedisHelper.GetAsync<string>(key);
}