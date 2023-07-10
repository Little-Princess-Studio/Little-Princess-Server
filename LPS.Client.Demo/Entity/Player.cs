// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity;

using Common.Debug;
using Common.Rpc.RpcProperty;
using Common.Rpc.Attribute;
using LPS.Client.Entity;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Entity.Component;

/// <summary>
/// Player class, from Untrusted.
/// </summary>
[EntityClass]
[Component(typeof(GamePropertyComponent))]
[Component(typeof(BagComponent))]
public class Player : ShadowClientEntity
{
    /// <summary>
    /// Player name.
    /// </summary>
    /// <returns>Name of the player.</returns>
    [RpcProperty(nameof(Player.Name))]
    public RpcShadowPlaintProperty<string> Name = new ();

    /// <summary>
    /// Id of the player.
    /// </summary>
    /// <returns>Id of the player in database.</returns>
    [RpcProperty(nameof(Player.Id))]
    public RpcShadowPlaintProperty<string> Id = new ();

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