// -----------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity;

using System.Collections.ObjectModel;
using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity.Component;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcStub;
using LPS.Common.Util;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// BaseEntity class.
/// </summary>
[RpcStubGenerator(typeof(RpcStubGenerator))]
public abstract class BaseEntity : ITypeIdSupport
{
    /// <summary>
    /// Gets or sets the mailbox of the entity.
    /// </summary>
    public MailBox MailBox { get; set; }

    /// <summary>
    /// Gets the property tree of the entity.
    /// </summary>
    protected Dictionary<string, RpcProperty>? PropertyTree => this.propertyTree;

    /// <summary>
    /// Gets or sets the dictionary of components associated with this entity.
    /// </summary>
    protected ReadOnlyDictionary<uint, ComponentBase> Components { get; set; } = null!;

    /// <summary>
    /// Gets or sets the dictionary that maps component names to their corresponding component type IDs.
    /// </summary>
    /// <remarks>
    /// The component type ID is a unique identifier for each component type.
    /// </remarks>
    protected ReadOnlyDictionary<string, uint> ComponentNameToComponentTypeId { get; set; } = null!;

    private readonly AsyncTaskGenerator<object?> rpcAsyncTaskWithoutResultGenerator;
    private readonly AsyncTaskGenerator<object?, System.Type> rpcAsyncTaskWithResultGenerator;

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
    /// Sets the entity RPC send handler.
    /// </summary>
    public Action<EntityRpc> OnSendEntityRpc { private get; set; } = null!;

    /// <summary>
    /// Sets the entity RPC callback send handler.
    /// </summary>
    public Action<EntityRpcCallBack> OnSendEntityRpcCallback { private get; set; } = null!;

    /// <summary>
    /// Sets the service RPC send handler.
    /// </summary>
    public Action<ServiceRpc> OnSendServiceRpc { private get; set; } = null!;

    /// <inheritdoc/>
    public uint TypeId { get; private set; }

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
    public virtual Task InitComponents()
    {
        var components = new Dictionary<uint, ComponentBase>();
        var componentNameToComponentTypeId = new Dictionary<string, uint>();

        var componentAttrs = this.GetType().GetCustomAttributes<ComponentAttribute>();
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

            components.Add(componentTypeId, component);
            componentNameToComponentTypeId.Add(componentName, componentTypeId);
        }

        this.Components = new ReadOnlyDictionary<uint, ComponentBase>(components);
        this.ComponentNameToComponentTypeId = new ReadOnlyDictionary<string, uint>(componentNameToComponentTypeId);

        foreach (var comp in this.Components.Values)
        {
            comp.OnInit();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the component of type T from the entity.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <returns>The component of type T.</returns>
    public virtual ValueTask<T> GetComponent<T>()
        where T : ComponentBase
    {
        var typeId = TypeIdHelper.GetId<T>();
        if (!this.Components.ContainsKey(typeId))
        {
            var e = new Exception("Component not found.");
            Logger.Error(e, $"Component {typeof(T).Name} not found in entity {this.GetType().Name}.");
            throw e;
        }

        return ValueTask.FromResult((T)this.Components[typeId]);
    }

    /// <summary>
    /// Gets the component of the specified type from the entity.
    /// </summary>
    /// <param name="componentType">The type of component to get.</param>
    /// <returns>The component of the specified type.</returns>
    public virtual ValueTask<ComponentBase> GetComponent(System.Type componentType)
    {
        var typeId = TypeIdHelper.GetId(componentType);
        if (!this.Components.ContainsKey(typeId))
        {
            var e = new Exception("Component not found.");
            Logger.Error(e, $"Component {componentType.Name} not found in entity {this.GetType().Name}.");
            throw e;
        }

        return ValueTask.FromResult(this.Components[typeId]);
    }

    /// <summary>
    /// Gets the component with the specified name from the entity.
    /// </summary>
    /// <param name="componentName">The name of the component to get.</param>
    /// <returns>The component with the specified name.</returns>
    public virtual ValueTask<ComponentBase> GetComponent(string componentName)
    {
        var typeId = this.ComponentNameToComponentTypeId[componentName];
        if (!this.Components.ContainsKey(typeId))
        {
            var e = new Exception("Component not found.");
            Logger.Error(e, $"Component {componentName} not found in entity {this.GetType().Name}.");
            throw e;
        }

        return ValueTask.FromResult(this.Components[typeId]);
    }

    /// <summary>
    /// Gets a stub for the specified RPC interface.
    /// </summary>
    /// <typeparam name="T">The type of the RPC interface.</typeparam>
    /// <returns>A stub for the specified RPC interface.</returns>
    /// <exception cref="Exception">Thrown when the specified type is not an interface.</exception>
    protected virtual T GetRpcStub<T>()
        where T : class, IRpcStub
    {
        var generator = RpcStubGeneratorManager.GetRpcStubGenerator(this.GetType());
        return generator.GetRpcStubImpl<T>(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEntity"/> class.
    /// </summary>
    protected BaseEntity()
    {
        this.TypeId = TypeIdHelper.GetId(this.GetType());
        this.rpcAsyncTaskWithoutResultGenerator = new()
        {
            OnGenerateAsyncId = this.IncreaseRpcIdCnt,
        };
        this.rpcAsyncTaskWithResultGenerator = new()
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
                    databaseId = RpcHelper.GetString(value);
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
        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id,
            rpcMethodName,
            this.MailBox,
            targetMailBox,
            notifyOnly,
            rpcType,
            args);
        this.OnSendEntityRpc.Invoke(rpcMsg);
    }

    /// <summary>
    /// Sends an RPC callback with the specified RPC ID, target mailbox, RPC type, and result object.
    /// </summary>
    /// <param name="rpcId">The ID of the RPC.</param>
    /// <param name="targetMailBox">The mailbox to send the RPC callback to.</param>
    /// <param name="rpcType">The type of the RPC.</param>
    /// <param name="result">The result object to send with the RPC callback.</param>
    public void SendRpcCallBackWithRpcId(
        uint rpcId,
        MailBox targetMailBox,
        RpcType rpcType,
        object? result)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var callback = RpcHelper.BuildEntityRpcCallBackMessage(rpcId, targetMailBox, rpcType, result);
        this.OnSendEntityRpcCallback(callback);
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
            this.rpcAsyncTaskWithoutResultGenerator.GenerateAsyncTask(
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);

        this.OnSendEntityRpc.Invoke(rpcMsg);
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
            this.rpcAsyncTaskWithResultGenerator.GenerateAsyncTask(
                typeof(T),
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);
        this.OnSendEntityRpc.Invoke(rpcMsg);

        var res = await task;
#pragma warning disable CS8600
#pragma warning disable CS8603
        return (T)res;
#pragma warning restore CS8603
#pragma warning restore CS8600
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
        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, true, rpcType, args);
        this.OnSendEntityRpc.Invoke(rpcMsg);
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
    /// Calls a service's random shard and executes the specified RPC method with the given arguments..
    /// </summary>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the RPC method to call.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CallServiceShardRandomly(string serviceName, string rpcMethodName, params object?[] args)
        => this.CallService(serviceName, rpcMethodName, true, args);

    /// <summary>
    /// Calls a service's random shard and executes the specified RPC method with the given arguments.
    /// </summary>
    /// <typeparam name="T">The return type of the RPC method.</typeparam>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the RPC method to execute.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the return value of the RPC method.</returns>
    public Task<T> CallServiceShardRandomly<T>(string serviceName, string rpcMethodName, params object?[] args)
        => this.CallService<T>(serviceName, rpcMethodName, true, args);

    /// <summary>
    /// Calls a service shard by this entity's ID using the specified RPC method and arguments.
    /// </summary>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the RPC method to call.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CallServiceShardById(string serviceName, string rpcMethodName, params object?[] args)
        => this.CallService(serviceName, rpcMethodName, false, args);

    /// <summary>
    /// Calls a service shard by this entity's ID using the specified RPC method and arguments.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the RPC method to call.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    /// <returns>A task representing the result of the RPC method call.</returns>
    public Task<T> CallServiceShardById<T>(string serviceName, string rpcMethodName, params object?[] args)
        => this.CallService<T>(serviceName, rpcMethodName, false, args);

    /// <summary>
    /// Notifies a service shard randomly using the specified service name, RPC method name, and arguments.
    /// </summary>
    /// <param name="serviceName">The name of the service to notify.</param>
    /// <param name="rpcMethodName">The name of the RPC method to call.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    public void NotifyServiceShardRandomly(string serviceName, string rpcMethodName, params object?[] args)
        => this.NotifyService(serviceName, rpcMethodName, true, args);

    /// <summary>
    /// Notifies the service shard by this entity's ID.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="rpcMethodName">The name of the RPC method.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    public void NotifyServiceShardById(string serviceName, string rpcMethodName, params object?[] args)
        => this.NotifyService(serviceName, rpcMethodName, false, args);

    /// <summary>
    /// OnRpcCallBack is used to handle the rpc callback msg.
    /// </summary>
    /// <param name="callBack">Entity rpc callback message.</param>
    public void OnRpcCallBack(EntityRpcCallBack callBack)
    {
        if (this.IsDestroyed)
        {
            Logger.Warn("Entity already destroyed.");
            return;
        }

        var rpcId = callBack.RpcID;
        var result = callBack.Result;
        this.RpcAsyncCallBack(rpcId, result: result);
    }

    /// <summary>
    /// Called when a service RPC callback is received.
    /// </summary>
    /// <param name="serviceRpc">The service RPC object containing the callback data.</param>
    public void OnServiceRpcCallBack(ServiceRpcCallBack serviceRpc)
    {
        if (this.IsDestroyed)
        {
            Logger.Warn("Entity already destroyed.");
            return;
        }

        var rpcId = serviceRpc.RpcID;
        var result = serviceRpc.Result;
        this.RpcAsyncCallBack(rpcId, result);
    }

    private async Task CallService(string serviceName, string rpcMethodName, bool random, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen)
        {
            throw new Exception("Entity is frozen.");
        }

        var (task, id) =
            this.rpcAsyncTaskWithoutResultGenerator.GenerateAsyncTask(
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildServiceRpcMessage(
            id, serviceName, rpcMethodName, this.MailBox, random, false, ServiceRpcType.ServerToService, args);

        this.OnSendServiceRpc.Invoke(rpcMsg);
        await task;
    }

    private async Task<T> CallService<T>(string serviceName, string rpcMethodName, bool random, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen)
        {
            throw new Exception("Entity is frozen.");
        }

        var (task, id) =
            this.rpcAsyncTaskWithResultGenerator.GenerateAsyncTask(
                typeof(T),
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildServiceRpcMessage(
            id, serviceName, rpcMethodName, this.MailBox, random, false, ServiceRpcType.ServerToService, args);
        this.OnSendServiceRpc.Invoke(rpcMsg);

        var res = await task;
#pragma warning disable CS8600
#pragma warning disable CS8603
        return (T)res;
#pragma warning restore CS8603
#pragma warning restore CS8600
    }

    private void NotifyService(string serviceName, string rpcMethodName, bool random, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen)
        {
            throw new Exception("Entity is frozen.");
        }

        var id = this.IncreaseRpcIdCnt();
        var rpcMsg = RpcHelper.BuildServiceRpcMessage(
            id, serviceName, rpcMethodName, this.MailBox, random, true, ServiceRpcType.ServerToService, args);
        this.OnSendServiceRpc.Invoke(rpcMsg);
    }

    private void RpcAsyncCallBack(uint rpcId, Any result)
    {
        if (this.rpcAsyncTaskWithResultGenerator.ContainsAsyncId(rpcId))
        {
            var returnType = this.rpcAsyncTaskWithResultGenerator.GetDataByAsyncTaskId(rpcId);
            var rpcArg = RpcHelper.ProtoBufAnyToRpcArg(result, returnType);
            this.rpcAsyncTaskWithResultGenerator.ResolveAsyncTask(rpcId, rpcArg);
        }
        else
        {
            this.rpcAsyncTaskWithoutResultGenerator.ResolveAsyncTask(rpcId, null!);
        }
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