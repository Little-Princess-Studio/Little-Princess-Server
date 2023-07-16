// -----------------------------------------------------------------------
// <copyright file="IPlayerStub.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.RpcStub;

using LPS.Common.Demo.Rpc;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Represents a stub for a player client.
/// </summary>
[RpcClientStub]
public interface IPlayerStub : IClientPlayerStub
{
    /// <summary>
    /// Notifies the client that a message has been received from the server.
    /// </summary>
    /// <param name="msg">The message to print.</param>
    [RpcStubNotifyOnly(nameof(IClientPlayerStub.PrintMessageFromServer))]
    void NotifyPrintMessageFromServer(string msg);
}