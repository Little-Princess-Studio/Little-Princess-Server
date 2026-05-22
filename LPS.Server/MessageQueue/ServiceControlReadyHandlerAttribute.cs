// -----------------------------------------------------------------------
// <copyright file="ServiceControlReadyHandlerAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Marks a method as the handler for one
/// <c>ServiceControlMessage.Ready</c> sub-case, dispatched by
/// <see cref="ServiceRemoteType"/> (Service / Server / Gate).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ServiceControlReadyHandlerAttribute : Attribute
{
    /// <summary>Gets the remote-type discriminator this method handles.</summary>
    public ServiceRemoteType Key { get; }

    /// <summary>Initializes a new instance of the <see cref="ServiceControlReadyHandlerAttribute"/> class.</summary>
    /// <param name="key">Discriminator.</param>
    public ServiceControlReadyHandlerAttribute(ServiceRemoteType key) => this.Key = key;
}
