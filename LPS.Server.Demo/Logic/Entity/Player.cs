// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Entity;

using Common.Debug;
using Common.Rpc.Attribute;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcProperty;
using LPS.Server.Entity;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Player is the real entity between server and client after login process.
/// </summary>
[EntityClass(IsDatabaseEntity = true)]
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
    /// <returns>Id of the player in database.</returns>
    [RpcProperty(nameof(Player.Id), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public RpcPlaintProperty<string> Id = new (string.Empty);

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

    /// <inheritdoc/>
    protected override async Task OnMigratedIn(MailBox originMailBox, string migrateInfo, Dictionary<string, string>? extraInfo)
    {
        await base.OnMigratedIn(originMailBox, migrateInfo, extraInfo);
        Logger.Info($"Player migrated in with account id {migrateInfo}");

        // initialize entity
        await this.LinkToDatabase(new Dictionary<string, string> { ["key"] = "accountId", ["value"] = migrateInfo });
    }
}