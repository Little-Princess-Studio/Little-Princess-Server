// -----------------------------------------------------------------------
// <copyright file="BaseService.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Service;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Util;

/// <summary>
/// Base class for all services in the LPS.
/// </summary>
public abstract class BaseService : ITypeIdSupport
{
    /// <summary>
    /// Gets or sets the shard number for this service.
    /// </summary>
    public uint Shard { get; set; }

    /// <summary>
    /// Gets or sets the mailbox of this service.
    /// </summary>
    public Common.Rpc.MailBox MailBox { get; set; }

    /// <inheritdoc/>
    public uint TypeId { get; }

    /// <summary>
    /// Sets the action to be executed when a service RPC callback is sent.
    /// </summary>
    /// <value>The action to be executed when a service RPC callback is sent.</value>
    public Action<ServiceRpcCallBack> OnSendServiceRpcCallBack { private get; set; } = null!;

    /// <summary>
    /// Sets the entity RPC send handler.
    /// </summary>
    public Action<EntityRpc> OnSendEntityRpc { private get; set; } = null!;

    /// <summary>
    /// Sets the service RPC send handler.
    /// </summary>
    public Action<ServiceRpc> OnSendServiceRpc { private get; set; } = null!;

    private readonly ConcurrentQueue<ServiceRpc> rpcQueue = new();

    private readonly AsyncTaskGenerator<object?> rpcAsyncTaskWithoutResultGenerator;
    private readonly AsyncTaskGenerator<object?, System.Type> rpcAsyncTaskWithResultGenerator;

    private bool stopFlag = false;
    private SandBox sandBox = null!;
    private uint rpcIdCnt;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseService"/> class.
    /// Sets the TypeId property to the unique identifier of the derived class.
    /// </summary>
    public BaseService()
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
    /// Starts the service.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Start()
    {
        this.sandBox = SandBox.Create(this.IoHandler);
        this.sandBox.Run();
        await this.OnStart();
    }

    /// <summary>
    /// This method is called when all services are ready.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task OnAllServiceReady()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for the associated sandbox to exit.
    /// </summary>
    public void WaitForExit() => this.sandBox.WaitForExit();

    /// <summary>
    /// Stops the service.
    /// </summary>
    public void Stop()
    {
        this.stopFlag = true;
        this.OnStop();
    }

    /// <summary>
    /// Send RPC call given a RPC id.
    /// </summary>
    /// <param name="rpcId">Rpc Id.</param>
    /// <param name="serviceManagerRpcId">Service manager rpc ID.</param>
    /// <param name="targetMailBox">Target entity's mailbox.</param>
    /// <param name="rpcType">Rpc Type.</param>
    /// <param name="result">Rpc result.</param>
    /// <exception cref="Exception">Throw exception if failed to send.</exception>
    public void SendCallBackWithRpcId(
        uint rpcId,
        uint serviceManagerRpcId,
        Common.Rpc.MailBox? targetMailBox,
        ServiceRpcType rpcType,
        object? result)
    {
        if (this.stopFlag)
        {
            throw new Exception("Service is stopped.");
        }

        if (rpcType == ServiceRpcType.ServerToService
            || rpcType == ServiceRpcType.HttpToService)
        {
            throw new Exception($"Invalid rpc type {rpcType}.");
        }

        var callback = ServiceHelper.BuildServiceRpcCallBackMessage(
            rpcId, targetMailBox, rpcType, serviceManagerRpcId, result);
        this.OnSendServiceRpcCallBack?.Invoke(callback);
    }

    /// <summary>
    /// Enqueues the specified ServiceRpc for processing.
    /// </summary>
    /// <param name="rpc">The ServiceRpc to enqueue.</param>
    public void EnqueueRpc(ServiceRpc rpc) => this.rpcQueue.Enqueue(rpc);

    /// <summary>
    /// This method is called when the service is starting up.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task OnStart()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// This method is called when the service is restart.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task OnRestart()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the service is stopped.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task OnStop()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invokes a RPC on the specified entity with its mailbox and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The return type of the remote method.</typeparam>
    /// <param name="targetMailBox">The mailbox of the entity to call RPC.</param>
    /// <param name="rpcMethodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<T> Call<T>(Common.Rpc.MailBox targetMailBox, string rpcMethodName, params object[] args)
    {
        var (task, id) =
            this.rpcAsyncTaskWithResultGenerator.GenerateAsyncTask(
                typeof(T),
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, RpcType.ServiceToEntity, args);
        this.OnSendEntityRpc.Invoke(rpcMsg);

        var res = await task;
#pragma warning disable CS8600
#pragma warning disable CS8603
        return (T)res;
#pragma warning restore CS8603
#pragma warning restore CS8600
    }

    /// <summary>
    /// Invokes a RPC on the specified entity with its mailbox and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <param name="targetMailBox">The mailbox of the entity to call RPC.</param>
    /// <param name="rpcMethodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Call(Common.Rpc.MailBox targetMailBox, string rpcMethodName, params object[] args)
    {
        var (task, id) =
            this.rpcAsyncTaskWithoutResultGenerator.GenerateAsyncTask(
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, false, RpcType.ServiceToEntity, args);

        this.OnSendEntityRpc.Invoke(rpcMsg);
        await task;
    }

    /// <summary>
    /// Notifies the specified entity with its mailbox by invoking a remote method with the specified name and arguments.
    /// </summary>
    /// <param name="targetMailBox">The mailbox of the entity to notify.</param>
    /// <param name="rpcMethodName">The name of the remote method to invoke.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    public void Notify(Common.Rpc.MailBox targetMailBox, string rpcMethodName, params object[] args)
    {
        var id = this.IncreaseRpcIdCnt();
        var rpcMsg = RpcHelper.BuildEntityRpcMessage(
            id, rpcMethodName, this.MailBox, targetMailBox, true, RpcType.ServiceToEntity, args);

        this.OnSendEntityRpc.Invoke(rpcMsg);
    }

    /// <summary>
    /// Invokes a remote method on the specified service with the service's id, given name and method name, and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The return type of the remote method.</typeparam>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<T> CallServiceShardById<T>(string serviceName, string rpcMethodName, params object[] args)
        => this.CallServiceShard<T>(serviceName, rpcMethodName, false, args);

    /// <summary>
    /// Invokes a remote method on the specified service with the given name, shard and method name, and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CallServiceShardById(string serviceName, string rpcMethodName, params object[] args)
        => this.CallServiceShard(serviceName, rpcMethodName, false, args);

    /// <summary>
    /// Calls a service shard randomly.
    /// </summary>
    /// <typeparam name="T">The return type of the RPC method.</typeparam>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="rpcMethodName">The name of the RPC method.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the return value of the RPC method.</returns>
    public Task<T> CallServiceShardRandomly<T>(string serviceName, string rpcMethodName, params object[] args)
        => this.CallServiceShard<T>(serviceName, rpcMethodName, true, args);

    /// <summary>
    /// Calls a service shard randomly using the specified service name, RPC method name, and arguments.
    /// </summary>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="rpcMethodName">The name of the RPC method to call.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CallServiceShardRandomly(string serviceName, string rpcMethodName, params object[] args)
        => this.CallServiceShard(serviceName, rpcMethodName, true, args);

    /// <summary>
    /// Notifies the specified service with the given name, shard and method name by invoking a remote method with the specified name and arguments.
    /// </summary>
    /// <param name="serviceName">The name of the service to notify.</param>
    /// <param name="shard">The shard of the service to notify.</param>
    /// <param name="rpcMethodName">The name of the remote method to invoke.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    public void NotifyServiceShardById(string serviceName, int shard, string rpcMethodName, params object[] args)
        => this.NotifyServiceShard(serviceName, rpcMethodName, false, args);

    /// <summary>
    /// Notifies a service shard randomly using RPC.
    /// </summary>
    /// <param name="serviceName">The name of the service to notify.</param>
    /// <param name="rpcMethodName">The name of the RPC method to call.</param>
    /// <param name="args">The arguments to pass to the RPC method.</param>
    public void NotifyServiceShardRandomly(string serviceName, string rpcMethodName, params object[] args)
        => this.NotifyServiceShard(serviceName, rpcMethodName, true, args);

    /// <summary>
    /// This method is called when a remote procedure call (RPC) is received by the service.
    /// </summary>
    /// <param name="callback">The RPC callback to handle.</param>
    public void OnServiceRpcCallBack(ServiceRpcCallBack callback)
    {
        var rpcId = callback.RpcID;
        this.RpcAsyncCallBack(rpcId, callback.Result);
    }

    private async Task<T> CallServiceShard<T>(string serviceName, string rpcMethodName, bool random, params object[] args)
    {
        var (task, id) =
            this.rpcAsyncTaskWithResultGenerator.GenerateAsyncTask(
        typeof(T),
        5000,
        (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildServiceRpcMessage(
            id, serviceName, rpcMethodName, this.MailBox, random, false, ServiceRpcType.ServiceToService, args);
        this.OnSendServiceRpc.Invoke(rpcMsg);

        var res = await task;
#pragma warning disable CS8600
#pragma warning disable CS8603
        return (T)res;
#pragma warning restore CS8603
#pragma warning restore CS8600
    }

    private async Task CallServiceShard(string serviceName, string rpcMethodName, bool random, params object[] args)
    {
        var (task, id) =
            this.rpcAsyncTaskWithoutResultGenerator.GenerateAsyncTask(
                5000,
                (rpcId) => new RpcTimeOutException(this, rpcId));

        var rpcMsg = RpcHelper.BuildServiceRpcMessage(
            id, serviceName, rpcMethodName, this.MailBox, random, false, ServiceRpcType.ServiceToService, args);
        this.OnSendServiceRpc.Invoke(rpcMsg);

        await task;
    }

    private void NotifyServiceShard(string serviceName, string rpcMethodName, bool random, params object[] args)
    {
        var id = this.IncreaseRpcIdCnt();
        var rpcMsg = RpcHelper.BuildServiceRpcMessage(
            id, serviceName, rpcMethodName, this.MailBox, random, false, ServiceRpcType.ServiceToService, args);
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

    private void IoHandler()
    {
        while (!this.stopFlag)
        {
            if (this.rpcQueue.TryDequeue(out var rpc))
            {
                try
                {
                    this.HandleRpc(rpc);
                }
                catch (System.Exception e)
                {
                    Logger.Error(e, "Error while handling RPC.");
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        Logger.Info("Service stopped.");
    }

    private void HandleRpc(ServiceRpc serviceRpc)
    {
        ServiceHelper.CallService(this, serviceRpc);
    }
}