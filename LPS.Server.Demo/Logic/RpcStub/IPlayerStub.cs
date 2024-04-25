// -----------------------------------------------------------------------
// <copyright file="IPlayerStub.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.RpcStub;

using LPS.Common.Demo.Rpc.ClientStub;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Rpc;

/// <summary>
/// Represents a stub for a player client.
/// </summary>
[RpcStubForServerClient]
public interface IPlayerStub : IPlayerClientStub
{
    /// <summary>
    /// Notifies the client that a message has been received from the server.
    /// </summary>
    /// <param name="msg">The message to print.</param>
    [RpcStubNotifyOnly(nameof(IPlayerClientStub.PrintMessageFromServer))]
    void NotifyPrintMessageFromServer(string msg);
}