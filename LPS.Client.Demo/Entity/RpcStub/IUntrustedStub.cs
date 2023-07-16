// -----------------------------------------------------------------------
// <copyright file="IUntrustedStub.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity.RpcStub;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Defines methods for communicating with an untrusted server.
/// </summary>
///
[RpcServerStub]
public interface IUntrustedStub : LPS.Common.Demo.Rpc.IServerUntrustedStub
{
    /// <summary>
    /// Notifies the server that a user has logged in.
    /// </summary>
    /// <param name="userName">User name.</param>
    /// <param name="passWord">Password.</param>
    [RpcStubNotifyOnly("LogIn")]
    void NotifyLogIn(string userName, string passWord);
}