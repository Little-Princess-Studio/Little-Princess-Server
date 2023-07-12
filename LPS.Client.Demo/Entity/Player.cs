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
using LPS.Client.Entity.Component;
using LPS.Client.Demo.Entity.Component;

/// <summary>
/// Player class, from Untrusted.
/// </summary>
[EntityClass]
[ClientComponent(typeof(GamePropertyComponent))]
[ClientComponent(typeof(BagComponent))]
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

    /// <inheritdoc/>
    public override Task OnLoaded()
    {
        Logger.Debug($"[Player] {(string)this.Name} loaded.");
        return base.OnLoaded();
    }

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

    /// <summary>
    /// Prints the components of the player, including the game property component and the bag component.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask PrintComponents()
    {
        var gamePropertyComp = await this.GetComponent<GamePropertyComponent>();
        var bagComp = await this.GetComponent<BagComponent>();

        Logger.Debug($"[GamePropertyComponent] hp: {(int)gamePropertyComp.Hp}, sp: {(int)gamePropertyComp.Sp}");

        var bagCnt = bagComp.Items.Val.Count;
        Logger.Debug($"[BagComponent] cnt: {bagCnt}");

        if (bagCnt > 0)
        {
            var item = bagComp.Items.Val[0];
            Logger.Debug($"[BagComponent] item: {(int)item.ItemId}, {(string)item.ItemName}");
        }
    }
}