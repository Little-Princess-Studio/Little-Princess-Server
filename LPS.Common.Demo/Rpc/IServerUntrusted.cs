// -----------------------------------------------------------------------
// <copyright file="IServerUntrusted.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Demo.Rpc;

using LPS.Common.Rpc;
using LPS.Common.Rpc.Attribute;

/// <summary>
/// Represents an interface for an untrusted server.
/// </summary>
[RpcServerStubAttribute]
public interface IServerUntrusted : IRpcStub
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
}