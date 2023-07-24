// -----------------------------------------------------------------------
// <copyright file="ServiceBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Service;

using System;
using System.Threading.Tasks;
using LPS.Common.Rpc;

/// <summary>
/// Base class for all services in the LPS.
/// </summary>
public abstract class ServiceBase
{
    /// <summary>
    /// Called when the service is started.
    /// </summary>
    public virtual void OnStart()
    {
    }

    /// <summary>
    /// Called when the service is restarted.
    /// </summary>
    public virtual void OnRestart()
    {
    }

    /// <summary>
    /// Called when the service is stopped.
    /// </summary>
    public virtual void OnStop()
    {
    }

    /// <summary>
    /// Invokes a RPC on the specified entity with its mailbox and returns a task that represents the asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The return type of the remote method.</typeparam>
    /// <param name="mailBox">The mailbox of the entity to call RPC.</param>
    /// <param name="methodName">The name of the remote method to call.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<T> Call<T>(MailBox mailBox, string methodName, params object[] args)
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
    public Task Call(MailBox mailBox, string methodName, params object[] args)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Notifies the specified entity with its mailbox by invoking a remote method with the specified name and arguments.
    /// </summary>
    /// <param name="mailBox">The mailbox of the entity to notify.</param>
    /// <param name="methodName">The name of the remote method to invoke.</param>
    /// <param name="args">The arguments to pass to the remote method.</param>
    public void Notify(MailBox mailBox, string methodName, params object[] args)
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
}