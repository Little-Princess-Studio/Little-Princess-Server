// -----------------------------------------------------------------------
// <copyright file="Untrusted.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity;

using LPS.Client.Demo.Entity.RpcStub;
using LPS.Client.Entity;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcStub;

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
    public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp = new();

    /// <summary>
    /// Test property.
    /// </summary>
    [RpcProperty(nameof(TestRpcPlaintPropStr))]
    public readonly RpcShadowPlaintProperty<string> TestRpcPlaintPropStr = new();

    private readonly IUntrustedStub serverRpc;

    /// <summary>
    /// Initializes a new instance of the <see cref="Untrusted"/> class.
    /// </summary>
    public Untrusted()
    {
        // cache rpc stub object
        this.serverRpc = this.GetRpcStub<IUntrustedStub>();
    }

    /// <summary>
    /// Try to login.
    /// </summary>
    /// <returns>If succeed to login.</returns>
    public Task Login()
    {
        this.serverRpc.NotifyLogIn("demo", "123456");
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientStub)]
    public override async ValueTask OnMigrated(MailBox targetMailBox, string migrateInfo, string targetEntityClassName)
    {
        Logger.Debug(
            $"[OnMigrated] migrate from {ClientGlobal.ShadowClientEntity.MailBox} to {targetMailBox}, class name: {targetEntityClassName}");
        var shadowEntity = await RpcClientHelper.CreateClientEntity(targetEntityClassName);
        shadowEntity.OnSendEntityRpc = Client.Instance.Send;
        shadowEntity.MailBox = targetMailBox;
        shadowEntity.BindServerMailBox();

        // give up Untrusted to Player.
        ClientGlobal.ShadowClientEntity = shadowEntity;

        await base.OnMigrated(targetMailBox, migrateInfo, targetEntityClassName);
    }

    /// <summary>
    /// Test change property.
    /// </summary>
    /// <returns>Async value task.</returns>
    public ValueTask TestChange() =>
        this.serverRpc.TestChange();

    /// <summary>
    /// Test change prop.
    /// </summary>
    /// <param name="prop">Value to change.</param>
    /// <returns>Async value task.</returns>
    public ValueTask ChangeProp(string prop) =>
        this.serverRpc.ChangeProp(prop);

    /// <summary>
    /// Calls the service echo method with the specified message and logs the response.
    /// </summary>
    /// <param name="msg">The message to send to the service.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CallServiceEcho(string msg)
    {
        var res = await this.serverRpc.CallServiceEcho(msg);
        Logger.Debug($"[CallServiceEcho]: {res}");
    }

    /// <summary>
    /// Calls the server's untrusted RPC service to echo the given message back to the client.
    /// </summary>
    /// <param name="msg">The message to send to the server.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CallServiceEchoWithCallBack(string msg)
    {
        Logger.Info("[CallServiceEchoWithCallBack]: waiting for server response...");
        await this.serverRpc.CallServiceEchoWithCallBack(msg);
        Logger.Info("[CallServiceEchoWithCallBack]: server response received.");
    }
}