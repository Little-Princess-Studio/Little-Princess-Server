// -----------------------------------------------------------------------
// <copyright file="Untrusted.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Entity;

using System.Threading.Tasks;
using Common.Debug;
using Common.Rpc.RpcStub;
using Common.Rpc.RpcProperty;
using Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Demo.Rpc.ServerStub;
using LPS.Server.Database;
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.RpcProperty;
using LPS.Server.Demo.Logic.Service;

/// <summary>
/// Untrusted class is the first created entity between client and server.
/// </summary>
[EntityClass]
public class Untrusted : ServerClientEntity, IServerUntrustedStub
{
    /// <summary>
    /// TestRpcProp.
    /// </summary>
    [RpcProperty(nameof(Untrusted.TestRpcProp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public readonly RpcComplexProperty<RpcList<string>> TestRpcProp = new (new RpcList<string>(3, "111"));

    /// <summary>
    /// TestRpcPlaintPropStr.
    /// </summary>
    [RpcProperty(
        nameof(Untrusted.TestRpcPlaintPropStr),
        RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public readonly RpcPlaintProperty<string> TestRpcPlaintPropStr = new ("Hello, LPS");

    /// <summary>
    /// Initializes a new instance of the <see cref="Untrusted"/> class.
    /// </summary>
    /// <param name="desc">Description string for constructing DistributeEntity.</param>
    public Untrusted(string desc)
        : base(desc)
    {
        Logger.Debug($"Untrusted created, desc : {desc}");
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientOnly)]
    public ValueTask TestChange()
    {
        this.TestRpcProp.Val.Add("222");
        this.TestRpcPlaintPropStr.Val = "Little Princess";
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientOnly)]
    public ValueTask ChangeProp(string prop)
    {
        this.TestRpcPlaintPropStr.Val = prop;
        Logger.Debug($"[ChangeProp] prop = {prop}");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Echo Rpc method.
    /// </summary>
    /// <param name="msg">Echo message.</param>
    /// <returns>Async value task.</returns>
    [RpcMethod(Authority.ClientOnly)]
    public ValueTask<string> Echo(string msg)
    {
        return ValueTask.FromResult("echo:" + msg);
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientOnly)]
    public async Task<bool> LogIn(string name, string password)
    {
        Logger.Debug($"[LogIn] {name} {password}");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
        {
            Logger.Warn("[LogIn] Invalid name or password");
            return false;
        }

        var (success, accountId) = await this.CheckPassword(name, password);
        if (!success)
        {
            Logger.Warn("[LogIn] Failed to login");
            return false;
        }

        Logger.Debug($"[LogIn] Wait for creating Player entity anywhere by account id {accountId}.");
        var mailbox =
            await RpcServerHelper.CreateServerClientEntityAnywhere(nameof(Player), string.Empty, this);

        Logger.Debug($"[LogIn] Player entity created, mailbox {mailbox}.");
        var res = await this.MigrateTo(mailbox, accountId, nameof(Player));
        return res;
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientOnly)]
    public async Task<string> CallServiceEcho(string msg)
    {
        var res = await this.CallServiceShardRandomly<string>("EchoService", "Echo", msg);
        Logger.Info($"[Untrusted] CallServiceEcho, msg -> {msg}, res -> {res}");
        return res;
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ClientOnly)]
    public async Task CallServiceEchoWithCallBack(string msg)
    {
        await this.CallServiceShardRandomly<string>(nameof(EchoService), nameof(EchoService.EchoWithCallBackToEntity), this.MailBox, msg);
        Logger.Info($"[Untrusted] CallServiceEchoWithCallBack, msg -> {msg}");
    }

    /// <inheritdoc/>
    [RpcMethod(Authority.ServiceOnly)]
    public ValueTask OnCallServiceEchoWithCallBack(string msg)
    {
        Logger.Info($"[Untrusted] OnCallServiceEchoWithCallBack, msg -> {msg}");
        return ValueTask.CompletedTask;
    }

    private async Task<(bool Success, string AccountId)> CheckPassword(string name, string password)
    {
        var res = await DbHelper.CallDbApi<(string Password, string AccountId)>(nameof(DbApi.DbApi.QueryAccountByUserName), name);

        if (res.Password != password)
        {
            Logger.Warn($"[LogIn][CheckPassword] Password not match for {name}");
            return (false, string.Empty);
        }

        Logger.Info($"[LogIn][CheckPassword] Login success. {res}");

        return (true, res.AccountId);
    }
}