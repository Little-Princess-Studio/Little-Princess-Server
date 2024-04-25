// -----------------------------------------------------------------------
// <copyright file="IUntrustedStub.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity.RpcStub;

using LPS.Client.Rpc;
using LPS.Common.Demo.Rpc.ServerStub;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Defines methods for communicating with an untrusted server.
/// </summary>
[RpcStubForShadowClient]
public interface IUntrustedStub : IUntrustedServerStub
{
    /// <summary>
    /// Notifies the server that a user has logged in.
    /// </summary>
    /// <param name="userName">User name.</param>
    /// <param name="passWord">Password.</param>
    [RpcStubNotifyOnly(nameof(IUntrustedServerStub.LogIn))]
    void NotifyLogIn(string userName, string passWord);
}