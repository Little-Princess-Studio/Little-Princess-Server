// -----------------------------------------------------------------------
// <copyright file="WebMgrHandlerAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;

/// <summary>
/// Marks an instance method as the handler for one WebManager round-trip.
/// <see cref="WebMgrDispatcher.ScanAndRegister"/> finds every method bearing
/// this attribute and wires it to the matching <see cref="WebMgrEndpoints"/>
/// entry; the reply routing key is auto-resolved from
/// <see cref="WebMgrEndpoints.RequestToReply"/>.
/// <para>Required signature: <c>JToken? Handler(JToken body)</c>.
/// Returning <c>null</c> publishes nothing (used to silently ignore
/// requests addressed to a different instance, e.g. a Gate that is not the
/// requested gateId).</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WebMgrHandlerAttribute : Attribute
{
    /// <summary>Gets the request routing key (typically a constant from <see cref="WebMgrEndpoints"/>).</summary>
    public string RequestRoutingKey { get; }

    /// <summary>Initializes a new instance of the <see cref="WebMgrHandlerAttribute"/> class.</summary>
    /// <param name="requestRoutingKey">Request routing key from <see cref="WebMgrEndpoints"/>.</param>
    public WebMgrHandlerAttribute(string requestRoutingKey)
    {
        this.RequestRoutingKey = requestRoutingKey;
    }
}
