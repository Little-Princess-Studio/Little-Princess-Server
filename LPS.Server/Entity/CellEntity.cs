// -----------------------------------------------------------------------
// <copyright file="CellEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Rpc;

/// <summary>
/// Cell entity is the entity to contains other entities, which has a Tick callback from server.
/// It's usually used for the main scene logic (such as game room/battle field).
/// </summary>
[EntityClass]
public abstract class CellEntity : DistributeEntity
{
    /// <summary>
    /// Gets the entities mapping in this cell, whose pair is (id, distribute entity).
    /// </summary>
    public readonly Dictionary<string, DistributeEntity> Entities = new();

    /// <summary>
    /// Gets callback When a entity left this cell.
    /// </summary>
    public Action<DistributeEntity> EntityLeaveCallBack { private get; init; } = null!;

    /// <summary>
    /// Gets callback when a entity enters this cell.
    /// </summary>
    public Action<DistributeEntity, MailBox> EntityEnterCallBack { private get; init; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="CellEntity"/> class.
    /// </summary>
    /// <param name="desc">Description string.</param>
    public CellEntity(string desc)
    {
    }

    /// <summary>
    /// Manually add an entity to the cell.
    /// </summary>
    /// <param name="entity">Entity to add.</param>
    public void ManuallyAdd(DistributeEntity entity)
    {
        entity.Cell = this;
        this.Entities.Add(entity.MailBox.Id, entity);
    }

    /// <summary>
    /// Get a distribute entity by its id.
    /// </summary>
    /// <param name="entityId">Entity id.</param>
    /// <returns>Distribute entity. null if the entity not exists.</returns>
    public DistributeEntity? GetEntityById(string entityId)
    {
        this.Entities.TryGetValue(entityId, out var entity);
        return entity;
    }

    /// <summary>
    /// Rpc for require a transfer.
    /// </summary>
    /// <param name="entityMailBox">Mailbox of the entity wants to transfer into this cell.</param>
    /// <param name="entityClassName">Entity class name of the entity.</param>
    /// <param name="serialContent">Serialization content of the entity.</param>
    /// <param name="transferInfo">Transfer info of the entity.</param>
    /// <param name="gateMailBox">Mailbox of the gate which sends the request rpc.</param>
    /// <returns>Pair of (success, new mailbox of the entity transferred).</returns>
    [RpcMethod(Authority.ServerOnly)]
    public ValueTask<(bool ResultOfTransfer, MailBox NewMailbox)> RequireTransfer(
        MailBox entityMailBox,
        string entityClassName,
        string serialContent,
        string transferInfo,
        MailBox gateMailBox)
    {
        Logger.Debug($"transfer request: {entityMailBox} {entityClassName} {serialContent} {transferInfo}");

        var res = false;
        do
        {
            if (this.Entities.ContainsKey(entityMailBox.Id))
            {
                break;
            }

            var entity = RpcServerHelper.BuildEntityFromSerialContent(
                new MailBox(entityMailBox.Id, this.MailBox.Ip, this.MailBox.Port, this.MailBox.HostNum),
                entityClassName,
                serialContent);

            entity.Cell = this;
            entity.OnTransferred(transferInfo);
            this.OnEntityEnter(entity, gateMailBox);

            if (entity is ServerClientEntity serverClientEntity)
            {
                serverClientEntity.Client.Notify("OnTransfer", entity.MailBox);
            }

            res = true;
        }
        while (false);

        return ValueTask.FromResult((res, entityMailBox));
    }

    /// <summary>
    /// Server tick callback for this cell.
    /// </summary>
    public abstract void Tick();

    /// <summary>
    /// Callback when entity enters the cell.
    /// </summary>
    /// <param name="entity">Entity object.</param>
    /// <param name="gateMailBox">Gate mailbox, used for entity transfer.</param>
    public void OnEntityEnter(DistributeEntity entity, MailBox gateMailBox)
    {
        this.Entities.Add(entity.MailBox.Id, entity);
        this.EntityEnterCallBack.Invoke(entity, gateMailBox);
    }

    /// <summary>
    /// Callback when entity left the cell.
    /// </summary>
    /// <param name="entity">Entity object.</param>
    public void OnEntityLeave(DistributeEntity entity)
    {
        this.Entities.Remove(entity.MailBox.Id);
        this.EntityLeaveCallBack.Invoke(entity);
    }
}