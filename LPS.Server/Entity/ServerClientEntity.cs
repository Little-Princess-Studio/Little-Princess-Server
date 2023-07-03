// -----------------------------------------------------------------------
// <copyright file="ServerClientEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.InnerMessages;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// ServerClientEntity indicates an entity which both have server-side and client-side objects, these two objects
/// has communication where they can call each other and sync properties.
/// </summary>
[EntityClass]
public class ServerClientEntity : DistributeEntity
{
    /// <summary>
    /// Gets the client proxy.
    /// </summary>
    public ClientProxy Client { get; private set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerClientEntity"/> class.
    /// </summary>
    /// <param name="desc">Entity description string.</param>
    protected ServerClientEntity(string desc)
        : base(desc)
    {
    }

    private ServerClientEntity()
    {
    }

    /// <summary>
    /// Rpc for require transfer this entity to a cell.
    /// </summary>
    /// <param name="targetCellMailBox">Mailbox of target cell.</param>
    /// <param name="transferInfo">Transfer info.</param>
    /// <returns>Task.</returns>
    [RpcMethod(Authority.All)]
    public override Task TransferIntoCell(MailBox targetCellMailBox, string transferInfo)
    {
        // todo: serialContent is the serialized rpc property tree of entity
        Logger.Debug($"start transfer to {targetCellMailBox}");

        var serialContent = string.Empty;
        try
        {
            var gateMailBox = this.Client.GateConn.MailBox;

            this.Notify(
                targetCellMailBox,
                "RequireTransfer",
                this.MailBox,
                this.GetType().Name,
                serialContent,
                transferInfo,
                gateMailBox);

            this.IsFrozen = true;

            // this.Client.Notify("OnTransfer", mailbox);
            this.Cell.OnEntityLeave(this);

            // Logger.Debug($"transfer success, new mailbox {mailbox}");
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error when transfer to cell");
            throw;
        }

        this.IsDestroyed = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Bind the gate connection related to the client.
    /// </summary>
    /// <param name="gateConnection">Connection of the gate.</param>
    public void BindGateConn(Connection gateConnection)
    {
        this.Client = new ClientProxy(gateConnection, this);
    }

    /// <inheritdoc/>
    public override Task<bool> MigrateTo(
        MailBox targetMailBox,
        string migrateInfo,
        Dictionary<string, string>? extraInfo)
    {
        if (extraInfo == null || !extraInfo.ContainsKey("targetEntityClassName"))
        {
            throw new Exception("Invalid migrate info.");
        }

        return base.MigrateTo(targetMailBox, migrateInfo, extraInfo);
    }

    /// <summary>
    /// Migrate to another ServerClientEntity.
    /// </summary>
    /// <param name="targetMailBox">Target entity migrate to.</param>
    /// <param name="migrateInfo">Info of the migration.</param>
    /// <param name="targetEntityClassName">Target entity class name.</param>
    /// <returns>If the migration success.</returns>
    public async Task<bool> MigrateTo(MailBox targetMailBox, string migrateInfo, string targetEntityClassName)
    {
        var res1 = await this.MigrateTo(targetMailBox, migrateInfo, new Dictionary<string, string>
        {
            { "targetEntityClassName", targetEntityClassName },
        });

        var server = ServerGlobal.Server;
        var res2 = await server.NotifyGateUpdateServerClientEntityRegistration(
            this,
            this.MailBox,
            targetMailBox);

        return res1 && res2;
    }

    /// <inheritdoc/>
    protected override void OnMigratedOut(
        MailBox targetMailBox,
        string migrateInfo,
        Dictionary<string, string>? extraInfo)
    {
        this.Client.Notify("OnMigrated", targetMailBox, string.Empty, extraInfo!["targetEntityClassName"]);
    }

    /// <summary>
    /// Client proxy of the entity, which indicates the related client-side object.
    /// </summary>
    public class ClientProxy
    {
        /// <summary>
        /// The Owner of this client proxy.
        /// </summary>
        public readonly ServerClientEntity Owner;

        /// <summary>
        /// Gets the gate connection of the client.
        /// </summary>
        public Connection GateConn { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientProxy"/> class.
        /// </summary>
        /// <param name="gateConnection">Connection to the gate related to the client.</param>
        /// <param name="owner">Owner of the gate.</param>
        public ClientProxy(Connection gateConnection, ServerClientEntity owner)
        {
            this.GateConn = gateConnection;
            this.Owner = owner;
        }

        /// <summary>
        /// Start a RPC call to the client.
        /// </summary>
        /// <param name="methodName">Name of the RPC method.</param>
        /// <param name="args">Args list.</param>
        /// <typeparam name="T">Type of the RPC result.</typeparam>
        /// <returns>Result of the RPC.</returns>
        public Task<T> Call<T>(string methodName, params object?[] args)
        {
            return this.Owner.Call<T>(this.Owner.MailBox, methodName, RpcType.ServerToClient, args);
        }

        /// <summary>
        /// Start a RPC call to the client.
        /// </summary>
        /// <param name="methodName">Name of the RPC method.</param>
        /// <param name="args">Args list.</param>
        /// <returns>Result of the RPC.</returns>
        public Task Call(string methodName, params object?[] args)
        {
            return this.Owner.Call(this.Owner.MailBox, methodName, RpcType.ServerToClient, args);
        }

        /// <summary>
        /// Start a RPC notify to the client, which does not need the response from the client.
        /// </summary>
        /// <param name="methodName">Name of the RPC method.</param>
        /// <param name="args">Args list.</param>
        public void Notify(string methodName, params object?[] args)
        {
            this.Owner.Notify(this.Owner.MailBox, methodName, RpcType.ServerToClient, args);
        }
    }
}