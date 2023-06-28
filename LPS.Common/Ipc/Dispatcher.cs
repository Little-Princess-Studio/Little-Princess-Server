// -----------------------------------------------------------------------
// <copyright file="Dispatcher.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using LPS.Common.Debug;

/// <summary>
/// Message dispatcher.
/// </summary>
/// <typeparam name="TArg">Type of the handler arg.</typeparam>
public class Dispatcher<TArg>
{
    private readonly Dictionary<object, List<Action<TArg>>> callbacks = new();

    /// <summary>
    /// Register message.
    /// </summary>
    /// <param name="key">Message key.</param>
    /// <param name="callback">Handler of the message.</param>
    public void Register(IComparable key, Action<TArg> callback)
    {
        if (this.callbacks.ContainsKey(key))
        {
            this.callbacks[key].Add(callback);
        }
        else
        {
            this.callbacks[key] = new() { callback };
        }
    }

    /// <summary>
    /// Unregister message.
    /// </summary>
    /// <param name="key">Message key.</param>
    /// <param name="callback">Handler of the message.</param>
    public void Unregister(IComparable key, Action<TArg> callback)
    {
        if (this.callbacks.ContainsKey(key))
        {
            var callbackList = this.callbacks[key];
            callbackList.Remove(callback);
            if (callbackList.Count == 0)
            {
                this.callbacks.Remove(key);
            }
        }
    }

    /// <summary>
    /// Dispatch message.
    /// </summary>
    /// <param name="key">Message key.</param>
    /// <param name="args">Message arguments.</param>
    public void Dispatch(IComparable key, TArg args)
    {
        if (this.callbacks.ContainsKey(key))
        {
            this.callbacks[key].ForEach(cb => cb.Invoke(args));
        }
        else
        {
            Logger.Warn($"{key} not registered.");
        }
    }

    /// <summary>
    /// Clear all registered messages.
    /// </summary>
    public void Clear()
    {
        this.callbacks.Clear();
    }
}