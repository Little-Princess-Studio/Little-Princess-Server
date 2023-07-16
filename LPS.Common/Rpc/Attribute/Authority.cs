// -----------------------------------------------------------------------
// <copyright file="Authority.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

/// <summary>
/// Authority of the RPC method.
/// </summary>
public enum Authority
{
    /// <summary>
    /// RPC method can only be called inside server.
    /// </summary>
    ServerOnly = 0x00000001,

    /// <summary>
    /// RPC method can only be called from client to server.
    /// </summary>
    ClientOnly = 0x00000010,

    /// <summary>
    /// RPC method can only be called from server to client.
    /// </summary>
    ClientStub = 0x00000100,

    /// <summary>
    /// RPC method can be called both from server to client or server to server.
    /// </summary>
    All = ServerOnly | ClientOnly,
}