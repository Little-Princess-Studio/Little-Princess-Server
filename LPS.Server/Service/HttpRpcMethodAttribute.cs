// -----------------------------------------------------------------------
// <copyright file="HttpRpcMethodAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc.Service;

using System;

/// <summary>
/// Represents the type of HTTP request for an RPC method.
/// </summary>
public enum HttpRpcRequestType
{
#pragma warning disable SA1602 // Enumeration items should be documented
    Get,
    Post,
    Put,
    Delete,
#pragma warning restore SA1602 // Enumeration items should be documented
}

/// <summary>
/// Attribute used to mark a method as an HTTP RPC method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HttpRpcMethodAttribute : Attribute
{
    /// <summary>
    /// The name of the HTTP query.
    /// </summary>
    public readonly string HttpQueryName;

    /// <summary>
    /// The type of HTTP request for the RPC method.
    /// </summary>
    public readonly HttpRpcRequestType RequestType;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRpcMethodAttribute"/> class.
    /// </summary>
    /// <param name="httpQueryName">The name of the HTTP query.</param>
    /// <param name="requestType">The type of HTTP request for the RPC method.</param>
    public HttpRpcMethodAttribute(string httpQueryName, HttpRpcRequestType requestType)
    {
        this.HttpQueryName = httpQueryName;
        this.RequestType = requestType;
    }
}