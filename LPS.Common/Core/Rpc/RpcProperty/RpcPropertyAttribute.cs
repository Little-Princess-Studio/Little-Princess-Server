// -----------------------------------------------------------------------
// <copyright file="RpcPropertyAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Rpc.RpcProperty;

/// <summary>
/// Attribute used to tag a rpc property container as RpcProperty.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class RpcPropertyAttribute : Attribute
{
}