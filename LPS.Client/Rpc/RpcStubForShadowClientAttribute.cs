// -----------------------------------------------------------------------
// <copyright file="RpcStubForShadowClientAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc;

using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Attribute to mark a class as a Shadow Client RPC Stub.
/// </summary>
/// <remarks>
/// This attribute is used to mark a class as a Shadow Client RPC Stub in the RPC system.
/// It inherits from the RpcStubAttribute class.
/// </remarks>
public class RpcStubForShadowClientAttribute : RpcStubAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcStubForShadowClientAttribute"/> class.
    /// </summary>
    public RpcStubForShadowClientAttribute()
        : base(typeof(RpcStubForShadowClientEntityGenerator))
    {
    }
}