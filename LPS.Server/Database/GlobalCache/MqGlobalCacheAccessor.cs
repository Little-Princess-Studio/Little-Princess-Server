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
    public Task<long> GenerateNewGlobalId()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public T? GetNativeClient<T>()
        where T : class
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<bool> Initialize(string initString)
    {
        throw new System.NotImplementedException();
    }
}
