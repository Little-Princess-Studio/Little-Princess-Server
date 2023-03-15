// -----------------------------------------------------------------------
// <copyright file="RpcTimeOutException.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Entity;

/// <summary>
/// Rpc timeout exception.
/// </summary>
public class RpcTimeOutException : Exception
{
    /// <summary>
    /// Who send the Rpc.
    /// </summary>
    public readonly BaseEntity Who;

    /// <summary>
    /// Id of the timed out RPC request.
    /// </summary>
    public readonly uint RpcId;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcTimeOutException"/> class.
    /// </summary>
    /// <param name="who">Who.</param>
    /// <param name="rpcId">Rpc ID.</param>
    public RpcTimeOutException(BaseEntity who, uint rpcId)
        : base("Rpc time out.")
    {
        this.Who = who;
        this.RpcId = rpcId;
    }
}