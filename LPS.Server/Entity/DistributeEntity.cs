// -----------------------------------------------------------------------
// <copyright file="DistributeEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using LPS.Server.Database;
using LPS.Server.Database.Storage;
using Newtonsoft.Json.Linq;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Distribute entity is the entity which can be created on any server process.
/// Generally, we should only call/notify distribute entities by their mailbox via RPC call.
/// </summary>
[EntityClass]
public abstract class DistributeEntity : BaseEntity, ISendPropertySyncMessage
{
    /// <summary>
    /// Gets or sets the cell of the entity.
    /// </summary>
    public CellEntity Cell { get; set; } = null!;

    /// <summary>
    /// Gets or sets the sync message handler of the entity.
    /// </summary>
    public Action<bool, uint, RpcPropertySyncMessage>? SendSyncMessageHandler { get; set; }

    /// <summary>
    /// Gets or sets the database ID of the entity.
    /// </summary>
    public string DbId
    {
        get => this.databaseId;
        set
        {
            if (string.IsNullOrEmpty(this.databaseId))
            {
                this.databaseId = value;
            }
            else
            {
                var e = new Exception("Db id could not be set multiple times.");
                Logger.Error(e);
                throw e;
            }
        }
    }

    private string databaseId = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributeEntity"/> class.
    /// </summary>
    /// <param name="desc">Description string for constructing DistributeEntity.</param>
    protected DistributeEntity(string desc)
        : this()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributeEntity"/> class.
    /// </summary>
    protected DistributeEntity()
    {
        this.IsFrozen = true;
    }

    /// <inheritdoc/>
    public override void SetPropertyTree(Dictionary<string, RpcProperty> propertyTree)
    {
        base.SetPropertyTree(propertyTree);
        foreach (var (_, v) in this.PropertyTree!)
        {
            v.SendSyncMsgImpl = this;
        }
    }

    /// <inheritdoc/>
    public void SendSyncMsg(bool keepOrder, uint delayTime, RpcPropertySyncMessage syncMsg)
    {
        if (syncMsg == null)
        {
            throw new ArgumentNullException(nameof(syncMsg));
        }

        Logger.Debug($"[SendSyncMsg] {keepOrder} {delayTime} {syncMsg}");
        this.SendSyncMessageHandler?.Invoke(keepOrder, delayTime, syncMsg);
    }

    /// <summary>
    /// Do full properties sync.
    /// </summary>
    /// <param name="onSyncContentReady">Callback when sync ready.</param>
    public void FullSync(Action<string, Any> onSyncContentReady)
    {
        var treeDict = this.ToAny();

        onSyncContentReady.Invoke(this.MailBox.Id, treeDict);
    }

    /// <summary>
    /// Ack method of sync request.
    /// </summary>
    public void FullSyncAck()
    {
        // todo: timeout of sync
        this.IsFrozen = false;
    }

    /// <summary>
    /// Migrate current DistributeEntity to another DistributeEntity
    /// return ture if successfully migrate, otherwise false
    /// Steps for migrating to another DistributeEntity:
    ///
    /// 1. set origin entity status to frozen
    /// 2. send request rpc to target DistributeEntity and wait
    /// 3. target entity set entity status to frozen
    /// 4. target entity rebuild self with migrateInfo (OnMigratedIn is called)
    /// 5. destroy current origin entity.
    ///
    /// </summary>
    /// <param name="targetMailBox">Target entity migrate to.</param>
    /// <param name="migrateInfo">Info of the migration.</param>
    /// <param name="extraInfo">Extra migrate info.</param>
    /// <returns>If the migration success.</returns>
    public virtual async Task<bool> MigrateTo(MailBox targetMailBox, string migrateInfo, Dictionary<string, string>? extraInfo)
    {
        if (targetMailBox.CompareOnlyID(this.MailBox))
        {
            return false;
        }

        Logger.Info("[Migrate] step 1.");
        Logger.Info($"start migrate, from {this.MailBox} to {targetMailBox}");

        try
        {
            var res = await this.Call<bool>(
                targetMailBox,
                nameof(this.RequireMigrate),
                this.MailBox,
                migrateInfo,
                extraInfo);

            this.IsFrozen = true;

            if (!res)
            {
                this.IsFrozen = false;
                throw new Exception("Error when migrate distribute entity");
            }

            Logger.Info("[Migrate] step 3.");

            await this.OnMigratedOut(targetMailBox, migrateInfo, extraInfo);

            // destroy self
            this.Cell.OnEntityLeave(this);
            this.Destroy();

            return true;
        }
        catch (Exception e)
        {
            this.IsFrozen = false;
            Logger.Error(e, "Error when migrate distribute entity");
            throw;
        }
    }

    /// <summary>
    /// Rpc for request a migrate.
    /// </summary>
    /// <param name="originMailBox">Original entity who wants to migrate into this entity.</param>
    /// <param name="migrateInfo">Migrate info.</param>
    /// <param name="extraInfo">Extra migrate info.</param>
    /// <returns>If the migration success.</returns>
    [RpcMethod(authority: Authority.ServerOnly)]
    public async Task<bool> RequireMigrate(MailBox originMailBox, string migrateInfo, Dictionary<string, string>? extraInfo)
    {
        Logger.Info("[Migrate] step 2.");
        this.IsFrozen = true;
        try
        {
            await this.OnMigratedIn(originMailBox, migrateInfo, extraInfo);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to migrate in");
            return false;
        }
        finally
        {
            this.IsFrozen = false;
        }

        return true;
    }

    /// <summary>
    /// Steps for entity transfer to other server
    ///
    /// 1. set entity status to frozen
    /// 2. serialize entity
    /// 3. send request rpc to target cell and wait
    /// 4. remote cell creates a clone entity locally
    /// 5. remote clone entity rebuild
    /// 6. remote cell add new entity
    /// 7. remote server bind gate connection to created new entity
    /// 8. origin entity notify client change mailbox (if entity is ServerClientEntity)
    /// 9. destroy origin entity.
    ///
    /// </summary>
    /// <param name="targetCellMailBox">Target cell's mailbox this entity wants to transfer.</param>
    /// <param name="transferInfo">Tansfer infomation.</param>
    /// <returns>Task.</returns>
    /// <exception cref="Exception">Throw exception if failed to transfer.</exception>
    public virtual async Task TransferIntoCell(MailBox targetCellMailBox, string transferInfo)
    {
        if (targetCellMailBox.CompareOnlyID(this.Cell.MailBox))
        {
            return;
        }

        this.IsFrozen = true;

        // todo: serialContent is the serialized rpc property tree of entity
        Logger.Debug($"start transfer to {targetCellMailBox}");

        var serialContent = string.Empty;
        try
        {
            var (res, mailbox) = await this.Call<(bool, MailBox)>(
                targetCellMailBox,
                nameof(CellEntity.RequireTransfer),
                this.MailBox,
                this.GetType().Name,
                serialContent,
                transferInfo);

            if (!res)
            {
                this.IsFrozen = false;
                throw new Exception("Error when transfer to cell");
            }

            this.Cell.OnEntityLeave(this);
            this.Destroy();

            Logger.Debug($"transfer success, new mailbox {mailbox}");
        }
        catch (Exception e)
        {
            this.IsFrozen = false;
            Logger.Error(e, "Error when transfer to cell");
            throw;
        }
    }

    /// <summary>
    /// Callback when finishing transferring.
    /// </summary>
    /// <param name="transferInfo">Transfer info.</param>
    public virtual void OnTransferred(string transferInfo)
    {
    }

    /// <summary>
    /// Converts the entity's property tree to a protobuf Any object.
    /// </summary>
    /// <returns>The protobuf Any object containing the entity's property tree.</returns>
    protected Any ToAny()
    {
        var treeDict = new DictWithStringKeyArg();

        foreach (var (key, value) in this.PropertyTree!)
        {
            if (value.CanSyncToClient)
            {
                treeDict.PayLoad.Add(key, value.ToProtobuf());
            }
        }

        return Any.Pack(treeDict);
    }

    /// <summary>
    /// Loads the entity data from the database. For customizing loading, overwrite <see cref="LoadFromDatabase"/> to customize the loading behavior.
    /// </summary>
    /// <param name="queryInfo">The query information to query entity data from database, default is the query condition { keyName: keyValue }to find the data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task LinkToDatabase(Dictionary<string, string> queryInfo)
    {
        var collName = this.GetType().GetCustomAttribute<EntityClassAttribute>()?.Name;
        if (string.IsNullOrEmpty(collName))
        {
            var e = new Exception("No corresponding collection name found on entity class.");
            Logger.Error(e);
            throw e;
        }

        await this.LoadFromDatabase(collName, queryInfo);
    }

    /// <summary>
    /// Loads the entity data from the database. Override this method to customize the loading behavior.
    /// </summary>
    /// <param name="collectionName">The name of the collection to query entity data from database.</param>
    /// <param name="queryInfo">The query information to find the data, default is the key to find the data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual async Task LoadFromDatabase(string collectionName, Dictionary<string, string> queryInfo)
    {
        var entityData = await DbHelper.CallDbInnerApi(
            "LoadEntity",
            Any.Pack(new StringArg() { PayLoad = collectionName }),
            Any.Pack(new StringArg() { PayLoad = queryInfo["key"] }),
            Any.Pack(new StringArg() { PayLoad = queryInfo["value"] }));

        if (entityData.Is(NullArg.Descriptor))
        {
            var e = new Exception("Failed to load player data.");
            Logger.Error(e);
            throw e;
        }

        this.BuildPropertyTreeByContent(entityData, out var databaseId);
        this.DbId = databaseId;
    }

    /// <summary>
    /// Saves the entity data to the database. Override this method to customize the saving behavior.
    /// </summary>
    /// <param name="collectionName">The name of the collection to save entity data to database.</param>
    /// <param name="queryInfo">The query information to find the data, default is the key to update the data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual async Task SaveToDatabase(string collectionName, Dictionary<string, string> queryInfo)
    {
        var serialContent = this.ToAny();
        var res = await DbHelper.CallDbInnerApi(
            "SaveEntity",
            Any.Pack(new StringArg() { PayLoad = queryInfo["id"] }),
            serialContent);
        if (res.Is(BoolArg.Descriptor))
        {
            var succ = res.Unpack<BoolArg>().PayLoad;
            if (!succ)
            {
                Logger.Warn("Failed to save entity.");
            }
        }
    }

    /// <summary>
    /// Callback when migrated in.
    /// </summary>
    /// <param name="originMailBox">Original entity who wants to migrate into this entity.</param>
    /// <param name="migrateInfo">Migrate info.</param>
    /// <param name="extraInfo">Extra migrate info.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual Task OnMigratedIn(MailBox originMailBox, string migrateInfo, Dictionary<string, string>? extraInfo)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Callback when this entity migrate out.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity to migrate in.</param>
    /// <param name="migrateInfo">Migrate info.</param>
    /// <param name="extraInfo">Extra migrate info.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual Task OnMigratedOut(MailBox targetMailBox, string migrateInfo, Dictionary<string, string>? extraInfo)
    {
        return Task.CompletedTask;
    }
}