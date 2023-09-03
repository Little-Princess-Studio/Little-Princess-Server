// -----------------------------------------------------------------------
// <copyright file="EchoService.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Service;

using LPS.Common.Debug;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Service;

/// <summary>
/// Represents a service that echoes back any input it receives.
/// </summary>
[Service("EchoService", 5)]
public class EchoService : BaseService
{
    /// <summary>
    /// Echoes the input message.
    /// </summary>
    /// <param name="msg">The message to echo.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation that returns the echoed message.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public ValueTask<string> Echo(string msg)
    {
        Logger.Info($"EchoService.Echo {msg}");
        var res = $"recieved: {msg}";
        return ValueTask.FromResult(res);
    }
}