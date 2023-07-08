// -----------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity;

using System.Linq;
using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity.Component;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Util;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// BaseEntity class.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the mailbox of the entity.
    /// </summary>
    public MailBox MailBox { get; set; }

    /// <summary>
    /// Gets the property tree of the entity.
    /// </summary>
    protected Dictionary<string, RpcProperty>? PropertyTree => this.propertyTree;

    private readonly AsyncTaskGenerator<object> rpcBlankAsyncTaskGenerator;
    private readonly AsyncTaskGenerator<object, System.Type> rpcAsyncTaskGenerator;

    private readonly Dictionary<uint, ComponentBase> components = new();
    private readonly Dictionary<string, uint> componentNameToComponentTypeId = new();

    private Dictionary<string, RpcProperty>? propertyTree;

    /// <summary>
    /// Gets or sets a value indicating whether this entity has been destroyed.
    /// </summary>
    public bool IsDestroyed { get; protected set; }

    /// <summary>
    /// Gets or sets a value indicating whether this entity is frozen.
    /// If an entity is frozen, it can only send rpc to client.
    /// </summary>
    public bool IsFrozen { get; protected set; }

    /// <summary>
    /// Sets the RPC send handler.
    /// </summary>
    public Action<EntityRpc> OnSend { private get; set; } = null!;

    private uint rpcIdCnt;

    /// <summary>
    /// Sets property tree.
    /// </summary>
    /// <param name="propertyTree">Property tree dictionary.</param>
    public virtual void SetPropertyTree(Dictionary<string, RpcProperty> propertyTree)
    {
        this.propertyTree = propertyTree;
    }

    /// <summary>
    /// Initializes all components of the entity.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public async Task InitComponents()
    {
        var componentAttr = this.GetType().GetCustomAttributes<ComponentAttribute>();
        var componentToLoad = new List<ComponentBase>();
        foreach (var attr in componentAttr)
        {
            var componentType = attr.ComponentType;
            var component = (ComponentBase)Activator.CreateInstance(componentType)!;
            var componentName = string.IsNullOrEmpty(componentType.Name) ? attr.ComponentType.Name : componentType.Name;

            component.Init(this, componentName);
            var componentTypeId = TypeIdHelper.GetId(componentType);

            if (this.components.ContainsKey(componentTypeId))
            {
                Logger.Warn($"Component {componentType.Name} is already added to entity {this.GetType().Name}.");
                continue;
            }

            if (!attr.LazyLoad)
            {
                componentToLoad.Add(component);
            }

            this.components.Add(componentTypeId, component);
            this.componentNameToComponentTypeId.Add(componentName, componentTypeId);
        }

        await this.OnComponentsLoaded(componentToLoad);

        componentToLoad.ForEach(comp => comp.OnInit());
    }

    /// <summary>
    /// Gets the component of type T from the entity.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <returns>The component of type T. If the component is marked as `LazyLoad`, it will be loaded this time.</returns>
    public async ValueTask<T> GetComponent<T>()
        where T : ComponentBase
    {
        var typeId = TypeIdHelper.GetId<T>();
        var component = await this.GetComponentInternal(typeId);
        return (T)component;
    }

    /// <summary>
    /// Gets the component of the specified type from the entity.
    /// </summary>
    /// <param name="componentType">The type of component to get.</param>
    /// <returns>The component of the specified type.</returns>
    public async ValueTask<ComponentBase> GetComponent(System.Type componentType)
    {
        var typeId = TypeIdHelper.GetId(componentType);
        var component = await this.GetComponentInternal(typeId);
        return component;
    }

    /// <summary>
    /// Gets the component with the specified name from the entity.
    /// </summary>
    /// <param name="componentName">The name of the component to get.</param>
    /// <returns>The component with the specified name.</returns>
    public async ValueTask<ComponentBase> GetComponent(string componentName)
    {
        var typeId = this.componentNameToComponentTypeId[componentName];
        var component = await this.GetComponentInternal(typeId);
        return component;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEntity"/> class.
    /// </summary>
    protected BaseEntity()
    {
        this.rpcBlankAsyncTaskGenerator = new AsyncTaskGenerator<object>
        {
            OnGenerateAsyncId = this.IncreaseRpcIdCnt,
        };
        this.rpcAsyncTaskGenerator = new AsyncTaskGenerator<object, System.Type>
        {
            OnGenerateAsyncId = this.IncreaseRpcIdCnt,
        };
    }

    /// <summary>
    /// Builds the property tree of the entity from the given content.
    /// </summary>
    /// <param name="propertyTree">The content used to build the property tree.</param>
    /// <param name="databaseId">The ID of the entity in the database.</param>
    public void BuildPropertyTreeByContent(Any propertyTree, out string databaseId)
    {
        databaseId = string.Empty;
        if (propertyTree.Is(descriptor: DictWithStringKeyArg.Descriptor))
        {
            var content = propertyTree.Unpack<DictWithStringKeyArg>();

            foreach (var (key, value) in content.PayLoad)
            {
                if (key == "_id")
                {
                    databaseId = value.Unpack<StringArg>().PayLoad;
                    continue;
                }

                if (this.PropertyTree!.ContainsKey(key))
                {
                    RpcProperty? prop = this.PropertyTree[key];
                    prop.FromProtobuf(value);
                }
                else
                {
                    Debug.Logger.Warn($"Missing sync property {key} in {this.GetType()}");
                }
            }
        }
    }

    /// <summary>
    /// Serialize the entity.
    /// </summary>
    /// <returns>Serialization content.</returns>
    public virtual string Serialize()
    {
        return string.Empty;
    }

    /// <summary>
    /// Deserialize the entity.
    /// </summary>
    /// <param name="content">Serialization content.</param>
    public virtual void Deserialize(string content)
    {
    }

    /// <summary>
    /// Destroy the entity.
    /// </summary>
    public void Destroy()
    {
        this.IsDestroyed = true;
    }

    /// <summary>
    /// Send RPC call.
    /// </summary>
    /// <param name="targetMailBox">Target entity's mailbox.</param>
    /// <param name="rpcMethodName">Rpc method name.</param>
    /// <param name="notifyOnly">Only notify.</param>
    /// <param name="rpcType">Rpc Type.</param>
    /// <param name="args">Arg list.</param>
    /// <exception cref="Exception">Throw exception if failed to send.</exception>
    public void Send(
        MailBox targetMailBox,
        string rpcMethodName,
        bool notifyOnly,
        RpcType rpcType,
        params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var id = this.IncreaseRpcIdCnt();
        var rpcMsg = RpcHelper.BuildRpcMessage(
            id,
            rpcMethodName,
            this.MailBox,
            targetMailBox,
            notifyOnly,
            rpcType,
            args);
        this.OnSend.Invoke(rpcMsg);
    }

    /// <summary>
    /// Send RPC call given a RPC id.
    /// </summary>
    /// <param name="rpcId">Rpc Id.</param>
    /// <param name="targetMailBox">Target entity's mailbox.</param>
    /// <param name="rpcMethodName">Rpc method name.</param>
    /// <param name="notifyOnly">Only notify.</param>
    /// <param name="rpcType">Rpc Type.</param>
    /// <param name="args">Arg list.</param>
    /// <exception cref="Exception">Throw exception if failed to send.</exception>
    public void SendWithRpcId(
        uint rpcId,
        MailBox targetMailBox,
        string rpcMethodName,
        bool notifyOnly,
        RpcType rpcType,
        params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var rpcMsg = RpcHelper.BuildRpcMessage(
            rpcId,
            rpcMethodName,
            this.MailBox,
            targetMailBox,
            notifyOnly,
            rpcType,
            args);
        this.OnSend.Invoke(rpcMsg);
    }

    /// <summary>
    /// Call RPC method.
    /// BaseEntity.Call will return a promise,
    /// which always wait for remote git a callback and give caller a async result.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc method name.</param>
    /// <param name="rpcType">Rpc Type.</param>
    /// <param name="args">Arg list.</param>
    /// <returns>Task.</returns>
    /// <exception cref="Exception">Throw exception if failed to call.</exception>
    public async Task Call(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var (task, id) =
            this.rpcBlankAsyncTaskGenerator.GenerateAsyncTask(
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);

        this.OnSend.Invoke(rpcMsg);
        await task;
    }

    /// <summary>
    /// Call RPC method inside server.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc Name.</param>
    /// <param name="args">Arg list.</param>
    /// <returns>Task.</returns>
    public async Task Call(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
        await this.Call(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

    /// <summary>
    /// Call RPC method.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc method name.</param>
    /// <param name="rpcType">Rpc Type.</param>
    /// <param name="args">Arg list.</param>
    /// <typeparam name="T">Result type.</typeparam>
    /// <returns>Task with result.</returns>
    /// <exception cref="Exception">Throw exception if failed to call.</exception>
    public async Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var (task, id) =
            this.rpcAsyncTaskGenerator.GenerateAsyncTask(
                typeof(T),
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);
        this.OnSend.Invoke(rpcMsg);

        var res = await task;
        return (T)res;
    }

    /// <summary>
    /// Call RPC method inside server.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc Name.</param>
    /// <param name="args">Arg list.</param>
    /// <typeparam name="T">Type of result.</typeparam>
    /// <returns>Task.</returns>
    public async Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
        await this.Call<T>(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

    /// <summary>
    /// Send a notify RPC to the entity.
    /// BaseEntity.Notify will not return any promise and only send rpc message to remote.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc method name.</param>
    /// <param name="rpcType">Rpc Type.</param>
    /// <param name="args">Arg list.</param>
    /// <exception cref="Exception">Throw exception if failed to notify.</exception>
    public void Notify(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var id = this.IncreaseRpcIdCnt();
        var rpcMsg = RpcHelper.BuildRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, true, rpcType, args);
        this.OnSend.Invoke(rpcMsg);
    }

    /// <summary>
    /// Send notify RPC method inside server.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc Name.</param>
    /// <param name="args">Arg list.</param>
    public void Notify(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
        this.Notify(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

    /// <summary>
    /// OnResult is a special RPC method with special parameter.
    /// </summary>
    /// <param name="entityRpc">Entity rpc message.</param>
    [RpcMethod(Authority.All)]
    public void OnResult(EntityRpc entityRpc)
    {
        if (this.IsDestroyed)
        {
            Logger.Warn("Entity already destroyed.");
            return;
        }

        var rpcId = entityRpc.RpcID;
        this.RpcAsyncCallBack(rpcId, entityRpc);
    }

    /// <summary>
    /// This method is called after components are loaded from the database.
    /// </summary>
    /// <param name="loadedComponents">The list of loaded components.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual Task OnComponentsLoaded(IEnumerable<ComponentBase> loadedComponents)
    {
        return Task.CompletedTask;
    }

    private void RpcAsyncCallBack(uint rpcId, EntityRpc entityRpc)
    {
        if (this.rpcAsyncTaskGenerator.ContainsAsyncId(rpcId))
        {
            var returnType = this.rpcAsyncTaskGenerator.GetDataByAsyncTaskId(rpcId);
            var rpcArg = RpcHelper.ProtoBufAnyToRpcArg(entityRpc.Args[0], returnType);
            this.rpcAsyncTaskGenerator.ResolveAsyncTask(rpcId, rpcArg!);
        }
        else
        {
            this.rpcBlankAsyncTaskGenerator.ResolveAsyncTask(rpcId, null!);
        }
    }

    private async ValueTask<ComponentBase> GetComponentInternal(uint typeId)
    {
        if (!this.components.ContainsKey(typeId))
        {
            var e = new Exception($"Component not found.");
            Logger.Error(e);
            throw e;
        }

        var component = this.components[typeId];

        if (!component.IsLoaded)
        {
            await component.LoadFromDatabase();
        }

        return component;
    }

    private uint IncreaseRpcIdCnt() => this.rpcIdCnt++;

    /// <summary>
    /// Finalizes an instance of the <see cref="BaseEntity"/> class.
    /// </summary>
    ~BaseEntity()
    {
        Logger.Info("Entity destroyed");
    }
}