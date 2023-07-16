// -----------------------------------------------------------------------
// <copyright file="RpcStubNotifyOnlyAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

/// <summary>
/// Attribute to mark a method as a notify-only RPC stub method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RpcStubNotifyOnlyAttribute : System.Attribute
{
    /// <summary>
    /// Gets the name of the RPC method to notify.
    /// </summary>
    public string RpcMethodName { get; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcStubNotifyOnlyAttribute"/> class.
    /// </summary>
    /// <param name="rpcMethodName">The name of the RPC method to notify.</param>
    public RpcStubNotifyOnlyAttribute(string rpcMethodName)
    {
        this.RpcMethodName = rpcMethodName;
    }
}
