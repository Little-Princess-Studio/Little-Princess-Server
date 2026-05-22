// -----------------------------------------------------------------------
// <copyright file="ServiceControlHandlerAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Marks a method as the handler for one <see cref="ServiceControlMessage"/>
/// case.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ServiceControlHandlerAttribute : Attribute
{
    /// <summary>Gets the service-control discriminator this method handles.</summary>
    public ServiceControlMessage Key { get; }

    /// <summary>Initializes a new instance of the <see cref="ServiceControlHandlerAttribute"/> class.</summary>
    /// <param name="key">Discriminator.</param>
    public ServiceControlHandlerAttribute(ServiceControlMessage key) => this.Key = key;
}
