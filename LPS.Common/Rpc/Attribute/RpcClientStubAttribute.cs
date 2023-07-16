// -----------------------------------------------------------------------
// <copyright file="RpcClientStubAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

/// <summary>
/// Represents an attribute that is used to mark an interface as a stub for a remote procedure call (RPC) method.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public class RpcClientStubAttribute : System.Attribute
{
}