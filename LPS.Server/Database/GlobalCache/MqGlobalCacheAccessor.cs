// -----------------------------------------------------------------------
// <copyright file="MqGlobalCacheAccessor.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.GlobalCache;

using System.Threading.Tasks;

/// <summary>
/// IGlobalCache implementation for mq cache.
/// </summary>
public class MqGlobalCacheAccessor : IGlobalCache
{
    /// <inheritdoc/>
    public Task Clear()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public object GetNativeClient()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<long> Incr(string key)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<bool> Initialize(string initString)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<bool> Set(string key, long val)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<bool> Set(string key, string val)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    Task<string> IGlobalCache<string>.Get(string key)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    Task<long> IGlobalCache<long>.Get(string key)
    {
        throw new System.NotImplementedException();
    }
}