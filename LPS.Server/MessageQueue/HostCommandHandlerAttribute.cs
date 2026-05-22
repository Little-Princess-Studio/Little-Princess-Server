// -----------------------------------------------------------------------
// <copyright file="HostCommandHandlerAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Marks a method as the handler for one <see cref="HostCommandType"/>
/// case. Picked up by an <c>EnumDispatcher&lt;HostCommandType, HostCommand&gt;</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HostCommandHandlerAttribute : Attribute
{
    /// <summary>Gets the host command discriminator this method handles.</summary>
    public HostCommandType Key { get; }

    /// <summary>Initializes a new instance of the <see cref="HostCommandHandlerAttribute"/> class.</summary>
    /// <param name="key">Discriminator.</param>
    public HostCommandHandlerAttribute(HostCommandType key) => this.Key = key;
}
