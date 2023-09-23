// -----------------------------------------------------------------------
// <copyright file="EchoService.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Service;

using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Demo.Logic.Entity;
using LPS.Server.Rpc.Service;
using LPS.Server.Service;

/// <summary>
/// Represents a service that echoes back any input it receives.
/// </summary>
[Service(nameof(EchoService), 5)]
public class EchoService : BaseService
{
    /// <summary>
    /// Echoes the input message.
    /// </summary>
    /// <param name="msg">The message to echo.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation that returns the echoed message.</returns>
    [RpcMethod(Authority.ServerOnly)]
    [HttpRpcMethod("echo", HttpRpcRequestType.Get)]
    public ValueTask<string> Echo(string msg)
    {
        Logger.Info($"EchoService.Echo {msg}");
        var res = $"recieved: {msg}";
        return ValueTask.FromResult(res);
    }

    /// <summary>
    /// Sends a message to the specified entity mailbox and waits for a callback response.
    /// </summary>
    /// <param name="entityMailBox">The mailbox of the entity to send the message to.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation that returns the callback response.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public async Task EchoWithCallBackToEntity(MailBox entityMailBox, string message)
    {
        Logger.Info($"EchoService.EchoWithCallBackToEntity {message} start");

        var res = $"recieved: {message}";
        await this.Call(entityMailBox, nameof(Untrusted.OnCallServiceEchoWithCallBack), res);

        Logger.Info($"EchoService.EchoWithCallBackToEntity {message} end");
    }
}