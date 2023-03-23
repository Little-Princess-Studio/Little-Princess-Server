// -----------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity;

using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.InnerMessages.ProtobufDefs;
using LPS.Common.Rpc.RpcProperty;
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

    private readonly Dictionary<uint, (Action<object>, Type)> rpcDict = new();
    private readonly Dictionary<uint, Action> rpcBlankDict = new();
    private Dictionary<string, RpcProperty>? propertyTree;

    /// <summary>
    /// Sets property tree.
    /// </summary>
    /// <param name="propertyTree">Property tree dictionary.</param>
    public virtual void SetPropertyTree(Dictionary<string, RpcProperty> propertyTree)
    {
        this.propertyTree = propertyTree;
    }

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

        var id = this.rpcIdCnt++;
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
    /// Send RPC call given a RPC id..
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
    public Task Call(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var id = this.rpcIdCnt++;
        var rpcMsg = RpcHelper.BuildRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);

        var cancellationTokenSource = new CancellationTokenSource(5000);
        var source = new TaskCompletionSource();

        cancellationTokenSource.Token.Register(
            () =>
            {
                this.RemoveRpcRecord(id);
                source.TrySetException(new RpcTimeOutException(this, id));
            },
            false);

        this.rpcBlankDict[id] = () => source.TrySetResult();
        this.OnSend.Invoke(rpcMsg);

        return source.Task;
    }

    /// <summary>
    /// Call RPC method inside server.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc Name.</param>
    /// <param name="args">Arg list.</param>
    /// <returns>Task.</returns>
    public Task Call(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
        this.Call(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

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
    public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
    {
        if (this.IsDestroyed)
        {
            throw new Exception("Entity already destroyed.");
        }

        if (this.IsFrozen && rpcType != RpcType.ServerToClient)
        {
            throw new Exception("Entity is frozen.");
        }

        var id = this.rpcIdCnt++;
        var rpcMsg = RpcHelper.BuildRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);

        var cancellationTokenSource = new CancellationTokenSource(5000);
        var source = new TaskCompletionSource<T>();

        cancellationTokenSource.Token.Register(
            () =>
            {
                this.RemoveRpcRecord(id);
                source.TrySetException(new RpcTimeOutException(this, id));
            },
            false);

        this.rpcDict[id] = (res => source.TrySetResult((T)res), typeof(T));
        this.OnSend.Invoke(rpcMsg);

        return source.Task;
    }

    /// <summary>
    /// Call RPC method inside server.
    /// </summary>
    /// <param name="targetMailBox">Mailbox of target entity.</param>
    /// <param name="rpcMethodName">Rpc Name.</param>
    /// <param name="args">Arg list.</param>
    /// <typeparam name="T">Type of result.</typeparam>
    /// <returns>Task.</returns>
    public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
        this.Call<T>(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

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

        var id = this.rpcIdCnt++;
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

    private void RpcAsyncCallBack(uint rpcId, EntityRpc entityRpc)
    {
        // Logger.Debug($"[RpcAsyncCallBack] {entityRpc}");
        if (this.rpcDict.ContainsKey(rpcId))
        {
            var (callback, returnType) = this.rpcDict[rpcId];
            var rpcArg = RpcHelper.ProtoBufAnyToRpcArg(entityRpc.Args[0], returnType);
            callback.Invoke(rpcArg!);
            this.rpcDict.Remove(rpcId);
        }
        else
        {
            var callback = this.rpcBlankDict[rpcId];
            callback.Invoke();
            this.rpcBlankDict.Remove(rpcId);
        }
    }

    private void RemoveRpcRecord(uint rpcId)
    {
        if (this.rpcDict.ContainsKey(rpcId))
        {
            this.rpcDict.Remove(rpcId);
        }
        else
        {
            this.rpcBlankDict.Remove(rpcId);
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="BaseEntity"/> class.
    /// </summary>
    ~BaseEntity()
    {
        Logger.Info("Entity destroyed");
    }
}