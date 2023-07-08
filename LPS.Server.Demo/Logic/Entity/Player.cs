// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Entity;

using Common.Debug;
using Common.Rpc.Attribute;
using LPS.Common.Entity.Component;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcProperty;
using LPS.Server.Database;
using LPS.Server.Entity;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Player is the real entity between server and client after login process.
/// </summary>
[EntityClass(DbCollectionName = "player", IsDatabaseEntity = true)]
[Component(typeof(GamePropertyComponent), PropertyName = "GameProperty")]
[Component(typeof(BagComponent), PropertyName = "Bag")]
public class Player : ServerClientEntity
{
    /// <summary>
    /// Player name.
    /// </summary>
    /// <returns>Name of the player.</returns>
    [RpcProperty(nameof(Player.Name), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public RpcPlaintProperty<string> Name = new (string.Empty);

    /// <summary>
    /// Id of the player.
    /// </summary>
    /// <returns>Account id of the player in database.</returns>
    [RpcProperty(nameof(Player.AccountId), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public RpcPlaintProperty<string> AccountId = new (string.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// </summary>
    /// <param name="desc">Entity description.</param>
    public Player(string desc)
        : base(desc)
    {
    }

    /// <summary>
    /// Remote ping.
    /// </summary>
    /// <param name="content">Ping content.</param>
    /// <returns>Ping result.</returns>
    [RpcMethod(Authority.ClientOnly)]
    public Task<string> Ping(string content)
    {
        Logger.Info($"[Player] Ping: {content}");
        return Task.FromResult("Res: " + content);
    }

    /// <summary>
    /// Updates the game properties of the player.
    /// </summary>
    /// <param name="hp">The new value for the player's health points.</param>
    /// <param name="sp">The new value for the player's stamina points.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RpcMethod(Authority.ClientOnly)]
    public async Task UpdateGameProperty(int hp, int sp)
    {
        var props = await this.GetComponent<GamePropertyComponent>();
        props.Hp.Val = hp;
        props.Sp.Val = sp;
        Logger.Info($"[Player] UpdateGameProperty, hp -> {hp}, sp -> {sp}");
    }

    /// <inheritdoc/>
    protected override async Task OnMigratedIn(MailBox originMailBox, string migrateInfo, Dictionary<string, string>? extraInfo)
    {
        await base.OnMigratedIn(originMailBox, migrateInfo, extraInfo);
        Logger.Info($"Player migrated in with account id: {migrateInfo}");

        var playerId = await DbHelper.CallDbApi<string>(nameof(DbApi.DbApi.CreatePlayerIfNotExist), migrateInfo);
        Logger.Debug($"[OnMigratedIn] Player id: {playerId}");

        await this.LinkToDatabase(new Dictionary<string, string> { ["key"] = "AccountId", ["value"] = migrateInfo });
    }
}