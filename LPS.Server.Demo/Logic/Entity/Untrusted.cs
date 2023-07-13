﻿// -----------------------------------------------------------------------
// <copyright file="Untrusted.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Entity;

using System.Threading.Tasks;
using Common.Debug;
using Common.Rpc.Attribute;
using Common.Rpc.RpcProperty;
using Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Demo.Rpc;
using LPS.Server.Database;
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Untrusted class is the first created entity between client and server.
/// </summary>
[EntityClass]
public class Untrusted : ServerClientEntity, IServerUntrusted
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

    /// <summary>
    /// Login Rpc invoked from client to login.
    /// </summary>
    /// <param name="name">User name.</param>
    /// <param name="password">Password.</param>
    /// <returns>Async value task.</returns>
    [RpcMethod(Authority.ClientOnly)]
    public async Task<bool> LogIn(string name, string password)
    {
        Logger.Debug($"[LogIn] {name} {password}");
        var (success, accountId) = await this.CheckPassword(name, password);
        if (!success)
        {
            Logger.Warn("Failed to login");
            return false;
        }

        Logger.Debug($"[LogIn] Wait for creating Player entity anywhere by account id {accountId}.");
        var mailbox =
            await RpcServerHelper.CreateServerClientEntityAnywhere(nameof(Player), string.Empty, this);

        Logger.Debug($"[LogIn] Player entity created, mailbox {mailbox}.");
        var res = await this.MigrateTo(mailbox, accountId, nameof(Player));
        return res;
    }

    /// <summary>
    /// Check username and password from database.
    /// </summary>
    /// <param name="name">User name.</param>
    /// <param name="password">Password.</param>
    /// <returns>Async value task of the check result.</returns>
    public async Task<(bool Success, string AccountId)> CheckPassword(string name, string password)
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