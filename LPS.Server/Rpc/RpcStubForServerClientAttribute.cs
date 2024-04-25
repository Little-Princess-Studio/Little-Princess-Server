// -----------------------------------------------------------------------
// <copyright file="RpcStubForServerClientAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Attribute to mark a class as a Shadow Client RPC Stub.
/// </summary>
/// <remarks>
/// This attribute is used to mark a class as a Shadow Client RPC Stub in the RPC system.
/// It inherits from the RpcStubAttribute class.
/// </remarks>
public class RpcStubForServerClientAttribute : RpcStubAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcStubForServerClientAttribute"/> class.
    /// </summary>
    public RpcStubForServerClientAttribute()
        : base(typeof(RpcStubForServerClientEntityGenerator))
    {
    }
}