// -----------------------------------------------------------------------
// <copyright file="IClientPlayerStub.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Demo.Rpc.ClientStub;

using LPS.Client.Rpc;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Represents a client player stub.
/// </summary>
[RpcStubGenerator(typeof(RpcStubForShadowClientEntityGenerator))]
public interface IClientPlayerStub : IRpcStub
{
    /// <summary>
    /// Prints a message received from the server.
    /// </summary>
    /// <param name="msg">The message to print.</param>
    /// <returns>Value task.</returns>
    ValueTask PrintMessageFromServer(string msg);
}
