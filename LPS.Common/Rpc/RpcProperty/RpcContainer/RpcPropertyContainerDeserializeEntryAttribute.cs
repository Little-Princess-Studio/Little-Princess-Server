// -----------------------------------------------------------------------
// <copyright file="RpcPropertyContainerDeserializeEntryAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Attribute used to tag a static method of RpcContainer class as deserialization method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RpcPropertyContainerDeserializeEntryAttribute : System.Attribute
{
}