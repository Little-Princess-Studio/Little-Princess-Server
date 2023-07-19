// -----------------------------------------------------------------------
// <copyright file="DistributeEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Entity.Component;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using LPS.Common.Rpc.RpcStub;
using LPS.Common.Util;
using LPS.Server.Database;
using LPS.Server.Entity.Component;
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

        this.IsFrozen = false;
    }

    /// <summary>
    /// Do full properties sync for a specific component.
    /// </summary>
    /// <param name="componentName">The name of the component to sync.</param>
    /// <param name="onSyncContentReady">Callback when sync is ready.</param>
    public void ComponentSync(string componentName, Action<string, Any> onSyncContentReady)
    {
        this.GetComponent(componentName)
            .AsTask()
            .ContinueWith(t =>
        {
            var comp = t.Result;
            var treeDict = comp.Serialize();

            onSyncContentReady.Invoke(this.MailBox.Id, treeDict);
            this.IsFrozen = false;
        });
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
    /// Initializes all components of the distribute entity.
    /// Only the components that are tagged as non-lazy will be initialized.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public override Task InitComponents()
    {
        var components = new Dictionary<uint, ComponentBase>();
        var componentNameToComponentTypeId = new Dictionary<string, uint>();

        var componentAttrs = this.GetType().GetCustomAttributes<ServerComponentAttribute>();
        var componentsToLoad = new List<ComponentBase>();
        foreach (var attr in componentAttrs)
        {
            var componentType = attr.ComponentType;
            var component = (ComponentBase)Activator.CreateInstance(componentType)!;
            var componentName = string.IsNullOrEmpty(componentType.Name) ? attr.ComponentType.Name : componentType.Name;

            component.InitComponent(this, componentName);
            var componentTypeId = TypeIdHelper.GetId(componentType);

            if (components.ContainsKey(componentTypeId))
            {
                Logger.Warn($"Component {componentType.Name} is already added to entity {this.GetType().Name}.");
                continue;
            }

            component.ShouldLazyLoad = attr.LazyLoad;

            if (!component.ShouldLazyLoad)
            {
                componentsToLoad.Add(component);
            }

            components.Add(componentTypeId, component);
            componentNameToComponentTypeId.Add(componentName, componentTypeId);
        }

        this.Components = new ReadOnlyDictionary<uint, ComponentBase>(components);
        this.ComponentNameToComponentTypeId = new ReadOnlyDictionary<string, uint>(componentNameToComponentTypeId);

        foreach (var comp in componentsToLoad)
        {
            comp.OnInit();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the component of type T from the entity.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <returns>The component of type T. If the component is marked as `LazyLoad`, it will be loaded this time.</returns>
    public override async ValueTask<T> GetComponent<T>()
    {
        var typeId = TypeIdHelper.GetId<T>();
        var component = await this.GetComponentInternal(typeId);
        return (T)component;
    }

    /// <summary>
    /// Gets the component of the specified type from the entity.
    /// </summary>
    /// <param name="componentType">The type of component to get. If the component is marked as `LazyLoad`, it will be loaded this time.</param>
    /// <returns>The component of the specified type.</returns>
    public override async ValueTask<ComponentBase> GetComponent(System.Type componentType)
    {
        var typeId = TypeIdHelper.GetId(componentType);
        var component = await this.GetComponentInternal(typeId);
        return component;
    }

    /// <summary>
    /// Gets the component with the specified name from the entity.
    /// </summary>
    /// <param name="componentName">The name of the component to get. If the component is marked as `LazyLoad`, it will be loaded this time.</param>
    /// <returns>The component with the specified name.</returns>
    public override async ValueTask<ComponentBase> GetComponent(string componentName)
    {
        if (!this.ComponentNameToComponentTypeId.ContainsKey(componentName))
        {
            var e = new Exception($"Component {componentName} not found.");
            Logger.Error(e);
            throw e;
        }

        var typeId = this.ComponentNameToComponentTypeId[componentName];
        var component = await this.GetComponentInternal(typeId);
        return component;
    }

    /// <summary>
    /// Gets a value indicating whether this entity is a database entity.
    /// </summary>
    public bool IsDatabaseEntity =>
        this.GetType().GetCustomAttribute<EntityClassAttribute>()?.IsDatabaseEntity ?? false;

    /// <summary>
    /// Gets the name of the database collection for this entity.
    /// </summary>
    /// <returns>The name of the database collection for this entity.</returns>
    public string? GetCollectionName()
    {
        if (!this.IsDatabaseEntity)
        {
            throw new Exception($"This entity of {this.GetType().FullName} is not a database entity.");
        }

        var attr = this.GetType().GetCustomAttribute<EntityClassAttribute>()!;
        var collName = attr?.DbCollectionName ?? attr?.Name;
        return collName;
    }

    /// <summary>
    /// Loads non-lazy components from the database.
    /// </summary>
    /// <param name="nonLazyComponents">The list of non-lazy components to load.</param>
    /// <returns>Task.</returns>
    protected async Task LoadNonLazyComponents(IEnumerable<ComponentBase> nonLazyComponents)
    {
        var componentsAny = await this.BatchLoadComponentsFromDatabase(nonLazyComponents);
        var componentsDict = componentsAny
            .Unpack<DictWithStringKeyArg>()
            .PayLoad
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value);

        foreach (var comp in nonLazyComponents)
        {
            if (componentsDict.ContainsKey(comp.Name))
            {
                comp.Deserialize(componentsDict[comp.Name]);
                await comp.OnLoadComponentData();
            }
            else
            {
                Logger.Warn($"Component {comp.Name} not found in database.");
            }
        }
    }

    /// <summary>
    /// Batch loads the components from the database.
    /// </summary>
    /// <param name="componentList">The list of components to load.</param>
    /// <returns>The protobuf Any object containing the loaded components.</returns>
    /// <exception cref="Exception">Thrown when failed to load components from the database.</exception>
    protected async Task<Any> BatchLoadComponentsFromDatabase(IEnumerable<ComponentBase> componentList)
    {
        string? collName = this.GetCollectionName();
        if (string.IsNullOrEmpty(collName))
        {
            var e = new Exception("No corresponding collection name found on entity class.");
            Logger.Error(e);
            throw e;
        }

        var components = new ListArg();

        foreach (var comp in componentList)
        {
            components.PayLoad.Add(RpcHelper.GetRpcAny(comp.Name));
        }

        var res = await DbHelper.CallDbInnerApi(
            "BatchLoadComponents",
            RpcHelper.GetRpcAny(collName),
            RpcHelper.GetRpcAny(this.DbId),
            Any.Pack(components));

        if (res.Is(NullArg.Descriptor))
        {
            var e = new Exception("Failed to load components from database");
            Logger.Error(e);
            throw e;
        }

        return res;
    }

    /// <summary>
    /// Batch saves the components to the database.
    /// </summary>
    /// <param name="componentList">The list of components to save.</param>
    /// <returns>A boolean indicating whether the save operation was successful or not.</returns>
    protected async Task<bool> BatchSaveComponentsToDatabase(IEnumerable<ComponentBase> componentList)
    {
        string? collName = this.GetCollectionName();
        if (string.IsNullOrEmpty(collName))
        {
            var e = new Exception("No corresponding collection name found on entity class.");
            Logger.Error(e);
            throw e;
        }

        var components = new DictWithStringKeyArg();
        foreach (var comp in componentList)
        {
            var compToAny = comp.Serialize();
            components.PayLoad.Add(comp.Name, compToAny);
        }

        var res = await DbHelper.CallDbInnerApi(
            "BatchSaveComponents",
            RpcHelper.GetRpcAny(collName),
            RpcHelper.GetRpcAny(this.DbId),
            Any.Pack(components));

        return res.Unpack<BoolArg>().PayLoad;
    }

    /// <summary>
    /// Converts the entity's property tree to a protobuf Any object.
    /// </summary>
    /// <returns>The protobuf Any object containing the entity's property tree.</returns>
    protected Any ToAny() => RpcHelper.SerializePropertyTree(this.PropertyTree!);

    /// <summary>
    /// Loads the entity data from the database. For customizing loading, overwrite <see cref="LoadFromDatabase"/> to customize the loading behavior.
    /// </summary>
    /// <param name="queryInfo">The query information to query entity data from database, default is the query condition { keyName: keyValue }to find the data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task LinkToDatabase(Dictionary<string, string> queryInfo)
    {
        if (!this.IsDatabaseEntity)
        {
            throw new Exception($"This entity of {this.GetType().FullName} is not a database entity.");
        }

        string? collName = this.GetCollectionName();
        if (string.IsNullOrEmpty(collName))
        {
            var e = new Exception("No corresponding collection name found on entity class.");
            Logger.Error(e);
            throw e;
        }

        // load entity first
        await this.LoadFromDatabase(collName, queryInfo);

        // then load non-lazy components
        var componentsToLoad = this.Components.Values.Where(comp => !comp.ShouldLazyLoad);
        await this.LoadNonLazyComponents(componentsToLoad);
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
            RpcHelper.GetRpcAny(collectionName),
            RpcHelper.GetRpcAny(queryInfo["key"]),
            RpcHelper.GetRpcAny(queryInfo["value"]));

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
            RpcHelper.GetRpcAny(queryInfo["id"]),
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

    private async ValueTask<ComponentBase> GetComponentInternal(uint typeId)
    {
        if (!this.Components.ContainsKey(typeId))
        {
            var e = new Exception($"Component not found.");
            Logger.Error(e);
            throw e;
        }

        var component = this.Components[typeId];

        if (this.IsDatabaseEntity && !component.IsLoaded)
        {
            await component.OnLoadComponentData();
        }

        component.OnInit();

        return component;
    }
}