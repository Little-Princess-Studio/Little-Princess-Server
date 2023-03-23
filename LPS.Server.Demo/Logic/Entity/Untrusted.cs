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
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Untrusted class is the first created entity between client and server.
/// </summary>
[EntityClass]
public class Untrusted : ServerClientEntity
{
    /// <summary>
    /// TestRpcProp.
    /// </summary>
    public readonly RpcComplexProperty<RpcList<string>> TestRpcProp = new (
        nameof(Untrusted.TestRpcProp),
        RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow,
        new RpcList<string>(3, "111"));

    /// <summary>
    /// TestRpcPlaintPropStr.
    /// </summary>
    public readonly RpcPlaintProperty<string> TestRpcPlaintPropStr = new (
        nameof(Untrusted.TestRpcPlaintPropStr),
        RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow,
        "Hello, LPS");

    /// <summary>
    /// Initializes a new instance of the <see cref="Untrusted"/> class.
    /// </summary>
    /// <param name="desc">Description string for constructing DistributeEntity.</param>
    public Untrusted(string desc)
        : base(desc)
    {
        Logger.Debug($"Untrusted created, desc : {desc}");
    }

    /// <summary>
    /// Test change property.
    /// </summary>
    /// <returns>Async value task.</returns>
    [RpcMethod(Authority.ClientOnly)]
    public ValueTask TestChange()
    {
        this.TestRpcProp.Val.Add("222");
        this.TestRpcPlaintPropStr.Val = "Little Princess";
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test change prop.
    /// </summary>
    /// <param name="prop">Value to change.</param>
    /// <returns>Async value task.</returns>
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
        if (!(await this.CheckPassword(name, password)))
        {
            Logger.Warn("Failed to login");
            return false;
        }

        Logger.Debug("[LogIn] Wait for creating entity anywhere.");
        var mailbox =
            await RpcServerHelper.CreateServerClientEntityAnywhere(nameof(Player), string.Empty, this);

        Logger.Debug($"[LogIn] entity created, mailbox {mailbox}.");

        // var res = await this.MigrateTo(mailbox, string.Empty);
        return true;
    }

    /// <summary>
    /// Check username and password from database.
    /// </summary>
    /// <param name="name">User name.</param>
    /// <param name="password">Password.</param>
    /// <returns>Async value task of the check result.</returns>
    public ValueTask<bool> CheckPassword(string name, string password)
    {
        // mock the validation of check name & password
        return ValueTask.FromResult(true);
    }
}