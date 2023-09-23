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

    /// <summary>
    /// Calls the EchoService's EchoWithCallBackToEntity method with a given message and returns the result.
    /// </summary>
    /// <param name="msg">The message to be passed to the EchoService.</param>
    /// <returns>The result of the EchoWithCallBackToEntity method.</returns>
    Task CallServiceEchoWithCallBack(string msg);

    /// <summary>
    /// Handles an echo message received from a service.
    /// </summary>
    /// <param name="msg">The message to echo.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask OnCallServiceEchoWithCallBack(string msg);
}