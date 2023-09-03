// -----------------------------------------------------------------------
// <copyright file="PlayerRosterService.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Service;

using CSRedis;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Database;
using LPS.Server.Rpc.Service;
using LPS.Server.Service;

/// <summary>
/// Represents a service for managing player rosters.
/// </summary>
[Service("PlayerRosterService", 1)]
public class PlayerRosterService : BaseService
{
    private const string RosterKeyName = "$_lps_player_roster";

    private readonly CSRedisClient redisClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerRosterService"/> class.
    /// </summary>
    public PlayerRosterService()
    {
        this.redisClient = DbHelper.FastGlobalCache.GetNativeClient<CSRedisClient>() ?? throw new Exception("Redis client is null");
    }

    /// <summary>
    /// Queries the mailbox of the player with the specified ID.
    /// </summary>
    /// <param name="playerId">The ID of the player to query.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the mailbox of the player with the specified ID.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public async Task<MailBox?> QueryPlayerMailBox(string playerId)
    {
        var res = await this.redisClient.HGetAsync(RosterKeyName, playerId);
        if (res is null)
        {
            return null;
        }

        return new MailBox(res);
    }

    /// <summary>
    /// Checks if a player with the specified ID exists in the player roster.
    /// </summary>
    /// <param name="playerId">The ID of the player to check.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the player exists in the player roster.</returns>
    [RpcMethod(Authority.ServerOnly)]
    [HttpRpcMethod("checkPlayerExist", HttpRpcRequestType.Get)]
    public Task<bool> CheckPlayerExist(string playerId) => this.redisClient.HExistsAsync(RosterKeyName, playerId);

    /// <summary>
    /// Registers a player with the specified ID and mailbox.
    /// </summary>
    /// <param name="playerId">The ID of the player to register.</param>
    /// <param name="mailBox">The mailbox to register.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the player was successfully registered.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public Task<bool> RegisterPlayer(string playerId, MailBox mailBox) => this.redisClient.HSetNxAsync(RosterKeyName, playerId, mailBox.ToString());

    /// <summary>
    /// Unregisters a player with the specified ID and mailbox.
    /// </summary>
    /// <param name="playerId">The ID of the player to unregister.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the player was successfully unregistered.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public async Task<bool> UnregisterPlayer(string playerId)
    {
        var res = await this.redisClient.HDelAsync(RosterKeyName, playerId);
        return res > 0;
    }
}
