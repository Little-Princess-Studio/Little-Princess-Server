// -----------------------------------------------------------------------
// <copyright file="ServiceBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Service;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// Base class for all services in the LPS.
/// </summary>
public abstract class ServiceBase
{
    /// <summary>
    /// Gets or sets the shard number for this service.
    /// </summary>
    public uint Shard { get; set; }

    private readonly ConcurrentQueue<ServiceRpc> rpcQueue = new();

    private bool stopFlag = false;

    private SandBox sandBox = null!;

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
    /// <param name="mailBox">The mailbox of the entity to call RPC.</param>
    /// <param name="methodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<T> Call<T>(Common.Rpc.MailBox mailBox, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Invokes a RPC on the specified entity with its mailbox and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <param name="mailBox">The mailbox of the entity to call RPC.</param>
    /// <param name="methodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Call(Common.Rpc.MailBox mailBox, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Notifies the specified entity with its mailbox by invoking a remote method with the specified name and arguments.
    /// </summary>
    /// <param name="mailBox">The mailbox of the entity to notify.</param>
    /// <param name="methodName">The name of the remote method to invoke.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    public void Notify(Common.Rpc.MailBox mailBox, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Invokes a remote method on the specified service with the given name, shard and method name, and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The return type of the remote method.</typeparam>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="shard">The shard of the service to call.</param>
    /// <param name="methodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<T> CallService<T>(string serviceName, int shard, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Invokes a remote method on the specified service with the given name, shard and method name, and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <param name="serviceName">The name of the service to call.</param>
    /// <param name="shard">The shard of the service to call.</param>
    /// <param name="methodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CallService(string serviceName, int shard, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Notifies the specified service with the given name, shard and method name by invoking a remote method with the specified name and arguments.
    /// </summary>
    /// <param name="serviceName">The name of the service to notify.</param>
    /// <param name="shard">The shard of the service to notify.</param>
    /// <param name="methodName">The name of the remote method to invoke.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task NotifyService(string serviceName, int shard, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

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

    private void HandleRpc(ServiceRpc rpc)
    {
        throw new NotImplementedException();
    }
}