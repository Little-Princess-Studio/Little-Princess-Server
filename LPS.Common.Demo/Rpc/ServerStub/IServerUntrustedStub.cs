// -----------------------------------------------------------------------
// <copyright file="IServerUntrustedStub.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Demo.Rpc.ServerStub;

using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Represents an interface for an rpc stub for Utrusted entity.
/// </summary>
public interface IServerUntrustedStub : IRpcStub
{
    /// <summary>
    /// Test change property.
    /// </summary>
    /// <returns>Async value task.</returns>
    ValueTask TestChange();

    /// <summary>
    /// Test change prop.
    /// </summary>
    /// <param name="prop">Value to change.</param>
    /// <returns>Async value task.</returns>
    ValueTask ChangeProp(string prop);

    /// <summary>
    /// Login Rpc invoked from client to login.
    /// </summary>
    /// <param name="name">User name.</param>
    /// <param name="password">Password.</param>
    /// <returns>Async value task.</returns>
    Task<bool> LogIn(string name, string password);

    /// <summary>
    /// Calls the Echo service with the specified message.
    /// </summary>
    /// <param name="message">The message to send to the Echo service.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the Echo service.</returns>
    Task<string> CallServiceEcho(string message);
}