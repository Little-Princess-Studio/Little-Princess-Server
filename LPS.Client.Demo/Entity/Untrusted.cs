// -----------------------------------------------------------------------
// <copyright file="Untrusted.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity;

using LPS.Client.Entity;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Untrusted class, entry of the connection between server and client.
/// </summary>
[EntityClass]
public class Untrusted : ShadowClientEntity
{
    /// <summary>
    /// Test property.
    /// </summary>
    public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp =
        new (nameof(Untrusted.TestRpcProp));

    /// <summary>
    /// Test property.
    /// </summary>
    public readonly RpcShadowPlaintProperty<string> TestRpcPlaintPropStr =
        new (nameof(Untrusted.TestRpcPlaintPropStr));

    /// <summary>
    /// Try to login.
    /// </summary>
    /// <returns>If succeed to login.</returns>
    public async Task<bool> Login()
    {
        return await this.Server.Call<bool>("LogIn", "username", "password");
    }
}