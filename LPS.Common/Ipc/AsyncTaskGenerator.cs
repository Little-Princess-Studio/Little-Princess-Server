// -----------------------------------------------------------------------
// <copyright file="AsyncTaskGenerator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using System.Collections.Concurrent;

/// <summary>
/// RpcAsyncGenerator is a helper class for generate async task.
/// </summary>
/// <typeparam name="TResult">Async result.</typeparam>
public class AsyncTaskGenerator<TResult>
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<TResult>> dictionary =
        new ConcurrentDictionary<int, TaskCompletionSource<TResult>>();

    private int asyncId;

    /// <summary>
    /// Generate a async task with unique token id.
    /// </summary>
    /// <returns>(AsyncTask, Token id) pair.</returns>
    public (Task<TResult> AsyncTaskTarget, int TokenId) GenerateAsyncTask()
    {
        var source = new TaskCompletionSource<TResult>();
        var taskId = Interlocked.Increment(ref this.asyncId);
        this.dictionary[taskId] = source;
        return (source.Task, taskId);
    }

    /// <summary>
    /// Resolve a task by token id.
    /// </summary>
    /// <param name="asyncId">Async task token id.</param>
    /// <param name="result">Result.</param>
    public void ResolveAsyncTask(int asyncId, TResult result)
    {
        if (!this.dictionary.ContainsKey(asyncId))
        {
            return;
        }

        var res = this.dictionary.TryGetValue(asyncId, out var source);
        while (!res)
        {
            res = this.dictionary.TryGetValue(asyncId, out source);
        }

        source !.TrySetResult(result);
    }
}