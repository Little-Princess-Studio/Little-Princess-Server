// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Entity;

using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Database;
using LPS.Server.Demo.Entity.Component;
using LPS.Server.Demo.Logic.RpcStub;
using LPS.Server.Demo.Logic.Service;
using LPS.Server.Entity;
using LPS.Server.Entity.Component;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Player is the real entity between server and client after login process.
/// </summary>
[EntityClass(DbCollectionName = "player", IsDatabaseEntity = true)]
[ServerComponent(typeof(GamePropertyComponent), PropertyName = "GameProperty")]
[ServerComponent(typeof(BagComponent), PropertyName = "Bag")]
public class Player : ServerClientEntity
{
    /// <summary>
    /// Player name.
    /// </summary>
    /// <returns>Name of the player.</returns>
    [RpcProperty(nameof(Player.Name), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public RpcPlaintProperty<string> Name = new(string.Empty);

    /// <summary>
    /// Id of the player.
    /// </summary>
    /// <returns>Account id of the player in database.</returns>
    [RpcProperty(nameof(Player.AccountId), RpcPropertySetting.Permanent)]
    public RpcPlaintProperty<string> AccountId = new(string.Empty);

    private readonly IPlayerStub playerStub;

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// </summary>
    /// <param name="desc">Entity description.</param>
    public Player(string desc)
        : base(desc)
    {
        // cache the playerStub
        this.playerStub = this.GetRpcStub<IPlayerStub>();
    }

    /// <inheritdoc/>
    public override async Task OnInit()
    {
        var databaseId = this.DbId;
        var res = await this.CallServiceShardById<bool>(
            nameof(PlayerRosterService),
            nameof(PlayerRosterService.RegisterPlayer),
            databaseId,
            this.MailBox);
        if (!res)
        {
            Logger.Warn($"playerId {databaseId} already exist, replace it.");
        }
        else
        {
            Logger.Info("Register player to roster success.");
        }
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
        this.playerStub.NotifyPrintMessageFromServer("Notification from server, player properties updated.");
    }

    /// <summary>
    /// QA-only: create a shadow of this Player on the other server in the
    /// cluster ("server0" creates on "server1" and vice versa), then mutate
    /// our Name property to a known value. The integration test
    /// (scripts/recovery/assert_shadow_sync.ps1) tails the cluster log to
    /// observe the shadow-server applying the sync.
    /// </summary>
    /// <param name="newName">New Name value to publish via the shadow path.</param>
    /// <returns>String describing what was done (for visibility in the test).</returns>
    [RpcMethod(Authority.ClientOnly)]
    public async Task<string> DebugCreateShadowAndMutate(string newName)
    {
        var oriServer = LPS.Server.ServerGlobal.Server;

        // Compute peer server's MailBox. In the default host0 config:
        // server0 = 127.0.0.1:12001, server1 = 127.0.0.1:12011. Whichever
        // server we're on, target the other one.
        var peerPort = oriServer.Port == 12001 ? 12011 : 12001;
        var peerMb = new MailBox(string.Empty, oriServer.Ip, peerPort, oriServer.HostNum);

        Logger.Info($"[DebugShadow] ori={this.MailBox} on {oriServer.Name}, creating shadow on {peerMb.Ip}:{peerMb.Port}.");
        var shadowMb = await oriServer.CreateShadowEntity(this.MailBox, peerMb, nameof(Player));
        Logger.Info($"[DebugShadow] shadow created at {shadowMb}. Mutating Name -> {newName}.");

        // Mutation: this triggers PropertySyncCommandList -> Gate -> fan-out
        // to subscribed shadow (the one we just created).
        this.Name.Val = newName;

        // Brief pause so the TimeCircle has a chance to flush. v1: ~25ms tick.
        await Task.Delay(150);

        return $"shadow={shadowMb.Id} mutated_name={newName}";
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