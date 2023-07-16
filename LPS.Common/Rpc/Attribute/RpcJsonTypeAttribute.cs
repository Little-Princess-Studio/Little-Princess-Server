// -----------------------------------------------------------------------
// <copyright file="RpcJsonTypeAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

/// <summary>
/// Tag a type is RPC Json type, this type will be automatically serialized/deserialized as json object.
/// Object should be used with Newtonsoft.Json.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class RpcJsonTypeAttribute : System.Attribute
{
}