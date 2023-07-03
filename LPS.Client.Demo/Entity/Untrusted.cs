// -----------------------------------------------------------------------
// <copyright file="Untrusted.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity;

using Common.Debug;
using Common.Rpc;
using Common.Rpc.RpcProperty;
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
    [RpcProperty(nameof(TestRpcProp))]
    public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp = new ();

    /// <summary>
    /// Test property.
    /// </summary>
    [RpcProperty(nameof(TestRpcPlaintPropStr))]
    public readonly RpcShadowPlaintProperty<string> TestRpcPlaintPropStr = new ();

    /// <summary>
    /// Try to login.
    /// </summary>
    /// <returns>If succeed to login.</returns>
    public Task<bool> Login()
    {
        this.Server.Notify("LogIn", "demo", "123456");
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientOnly)]
    public override ValueTask OnMigrated(MailBox targetMailBox, string migrateInfo, string targetEntityClassName)
    {
        Logger.Debug(
            $"[OnMigrated] migrate from {ClientGlobal.ShadowClientEntity.MailBox} to {targetMailBox}, class name: {targetEntityClassName}");
        var shadowEntity = RpcClientHelper.CreateClientEntity(targetEntityClassName);
        shadowEntity.OnSend = rpc => { Client.Instance.Send(rpc); };
        shadowEntity.MailBox = targetMailBox;
        shadowEntity.BindServerMailBox();

        // give up Untrusted to Player.
        ClientGlobal.ShadowClientEntity = shadowEntity;

        return base.OnMigrated(targetMailBox, migrateInfo, targetEntityClassName);
    }
}