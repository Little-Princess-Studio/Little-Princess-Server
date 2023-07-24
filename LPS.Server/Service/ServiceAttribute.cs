// -----------------------------------------------------------------------
// <copyright file="ServiceAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Service;

/// <summary>
/// Attribute to mark a class as a service.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class)]
public class ServiceAttribute : System.Attribute
{
    /// <summary>
    /// The name of the service.
    /// </summary>
    public readonly string ServiceName;

    /// <summary>
    /// The default number of shards for the service.
    /// </summary>
    public readonly int DefaultShardCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAttribute"/> class.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="defaultShardCount">The default number of shards for the service.</param>
    public ServiceAttribute(string serviceName, int defaultShardCount)
    {
        this.ServiceName = serviceName;
        this.DefaultShardCount = defaultShardCount;
    }
}