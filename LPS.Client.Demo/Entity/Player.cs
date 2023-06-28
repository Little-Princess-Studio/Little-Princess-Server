// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity;

using Common.Debug;
using Common.Rpc.Attribute;
using LPS.Client.Entity;

/// <summary>
/// Player class, from Untrusted.
/// </summary>
[EntityClass]
public class Player : ShadowClientEntity
{
    /// <summary>
    /// Test ping.
    /// </summary>
    /// <param name="content">Ping content.</param>
    /// <returns>ValueTask.</returns>
    public async ValueTask Ping(string content)
    {
        var res = await this.Server.Call<string>("Ping", content);
        Logger.Debug($"[Ping]: {res}");
    }
}