// -----------------------------------------------------------------------
// <copyright file="RpcMethodAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.Attribute;

/// <summary>
/// Tag a method as RPC method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RpcMethodAttribute : System.Attribute
{
    /// <summary>
    /// Autority of the tagged method.
    /// </summary>
    public readonly Authority Authority;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcMethodAttribute"/> class.
    /// </summary>
    /// <param name="authority">Autority of the tagged method.</param>
    public RpcMethodAttribute(Authority authority)
    {
        this.Authority = authority;
    }
}