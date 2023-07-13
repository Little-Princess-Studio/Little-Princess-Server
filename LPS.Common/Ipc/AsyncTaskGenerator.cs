// -----------------------------------------------------------------------
// <copyright file="AsyncTaskGenerator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using System.Collections.Concurrent;

#pragma warning disable SA1402

/// <summary>
/// RpcAsyncGenerator is a helper class for generate async task.
/// </summary>
/// <typeparam name="TResult">Async result.</typeparam>
public class AsyncTaskGenerator<TResult>
{
    /// <summary>
    /// Gets the costume async id generator.
    /// </summary>
    public Func<uint>? OnGenerateAsyncId { private get; init; }

    private readonly ConcurrentDictionary<uint, TaskCompletionSource<TResult>> dictionary =
        new();

    private uint asyncId;

    /// <summary>
    /// Check if async task id is recorded.
    /// </summary>
    /// <param name="asyncTaskId">Async task id.</param>
    /// <returns>If async task id exists.</returns>
    public bool ContainsAsyncId(uint asyncTaskId) => this.dictionary.ContainsKey(asyncTaskId);

    /// <summary>
    /// Generate a async task with unique token id.
    /// </summary>
    /// <returns>(AsyncTask, Token id) pair.</returns>
    public (Task<TResult> AsyncTaskTarget, uint TokenId) GenerateAsyncTask()
    {
        uint taskId = 0;
        if (this.OnGenerateAsyncId != null)
        {
            taskId = this.OnGenerateAsyncId.Invoke();
        }
        else
        {
            taskId = Interlocked.Increment(ref this.asyncId);
        }

        var source = new TaskCompletionSource<TResult>();
        this.dictionary[taskId] = source;
        return (source.Task, taskId);
    }

    /// <summary>
    /// Gets the task associated with the specified ID, if it exists.
    /// </summary>
    /// <param name="id">The ID of the task to retrieve.</param>
    /// <returns>The task associated with the specified ID, or null if no such task exists.</returns>
    public Task<TResult>? GetTaskById(uint id)
    {
        if (this.dictionary.TryGetValue(id, out var task))
        {
            return task.Task;
        }

        return null;
    }

    /// <summary>
    /// Generate a async task with unique token id, with timeout handler.
    /// </summary>
    /// <param name="timeoutMilliseconds">Max milliseconds to wait.</param>
    /// <param name="timeOutHandler">What exception to throw. </param>
    /// <returns>(AsyncTask, Token id) pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeOutHandler"/> should not be null.</exception>
    public (Task<TResult> AsyncTaskTarget, uint TokenId) GenerateAsyncTask(
        int timeoutMilliseconds,
        Func<uint, Exception> timeOutHandler)
    {
        if (timeOutHandler == null)
        {
            throw new ArgumentNullException(nameof(timeOutHandler));
        }

        var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);
        var source = new TaskCompletionSource<TResult>();
        var taskId = Interlocked.Increment(ref this.asyncId);

        cancellationTokenSource.Token.Register(() =>
        {
            var res = this.dictionary.TryRemove(taskId, out _);
            while (!res)
            {
                res = this.dictionary.TryRemove(taskId, out _);
            }

            source.TrySetException(timeOutHandler.Invoke(taskId));
        });

        this.dictionary[taskId] = source;
        return (source.Task, taskId);
    }

    /// <summary>
    /// Resolve a task by token id.
    /// </summary>
    /// <param name="asyncId">Async task token id.</param>
    /// <param name="result">Result.</param>
    public void ResolveAsyncTask(uint asyncId, TResult result)
    {
        if (!this.dictionary.ContainsKey(asyncId))
        {
            return;
        }

        var res = this.dictionary.TryRemove(asyncId, out var source);
        while (!res)
        {
            res = this.dictionary.TryRemove(asyncId, out source);
        }

        source !.TrySetResult(result);
    }
}

/// <summary>
/// RpcAsyncGenerator is a helper class for generate async task.
/// </summary>
public class AsyncTaskGenerator
{
    /// <summary>
    /// Gets the costume async id generator.
    /// </summary>
    public Func<uint>? OnGenerateAsyncId { private get; init; }

    private readonly ConcurrentDictionary<uint, TaskCompletionSource> dictionary =
        new();

    private uint asyncId;

    /// <summary>
    /// Check if async task id is recorded.
    /// </summary>
    /// <param name="asyncTaskId">Async task id.</param>
    /// <returns>If async task id exists.</returns>
    public bool ContainsAsyncId(uint asyncTaskId) => this.dictionary.ContainsKey(asyncTaskId);

    /// <summary>
    /// Generate a async task with unique token id.
    /// </summary>
    /// <returns>(AsyncTask, Token id) pair.</returns>
    public (Task AsyncTaskTarget, uint TokenId) GenerateAsyncTask()
    {
        uint taskId = 0;
        if (this.OnGenerateAsyncId != null)
        {
            taskId = this.OnGenerateAsyncId.Invoke();
        }
        else
        {
            taskId = Interlocked.Increment(ref this.asyncId);
        }

        var source = new TaskCompletionSource();
        this.dictionary[taskId] = source;
        return (source.Task, taskId);
    }

    /// <summary>
    /// Gets the task associated with the specified ID, if it exists.
    /// </summary>
    /// <param name="id">The ID of the task to retrieve.</param>
    /// <returns>The task associated with the specified ID, or null if no such task exists.</returns>
    public Task? GetTaskById(uint id)
    {
        if (this.dictionary.TryGetValue(id, out var task))
        {
            return task.Task;
        }

        return null;
    }

    /// <summary>
    /// Generate a async task with unique token id, with timeout handler.
    /// </summary>
    /// <param name="timeoutMilliseconds">Max milliseconds to wait.</param>
    /// <param name="timeOutHandler">What exception to throw. </param>
    /// <returns>(AsyncTask, Token id) pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeOutHandler"/> should not be null.</exception>
    public (Task AsyncTaskTarget, uint TokenId) GenerateAsyncTask(
        int timeoutMilliseconds,
        Func<uint, Exception> timeOutHandler)
    {
        if (timeOutHandler == null)
        {
            throw new ArgumentNullException(nameof(timeOutHandler));
        }

        var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);
        var source = new TaskCompletionSource();
        var taskId = Interlocked.Increment(ref this.asyncId);

        cancellationTokenSource.Token.Register(() =>
        {
            var res = this.dictionary.TryRemove(taskId, out _);
            while (!res)
            {
                res = this.dictionary.TryRemove(taskId, out _);
            }

            source.TrySetException(timeOutHandler.Invoke(taskId));
        });

        this.dictionary[taskId] = source;
        return (source.Task, taskId);
    }

    /// <summary>
    /// Resolve a task by token id.
    /// </summary>
    /// <param name="asyncId">Async task token id.</param>
    public void ResolveAsyncTask(uint asyncId)
    {
        if (!this.dictionary.ContainsKey(asyncId))
        {
            return;
        }

        var res = this.dictionary.TryRemove(asyncId, out var source);
        while (!res)
        {
            res = this.dictionary.TryRemove(asyncId, out source);
        }

        source !.TrySetResult();
    }
}

/// <summary>
/// RpcAsyncGenerator is a helper class for generate async task, with related data.
/// </summary>
/// <typeparam name="TResult">Async result.</typeparam>
/// <typeparam name="TData">Related data type.</typeparam>
public class AsyncTaskGenerator<TResult, TData>
#pragma warning restore SA1402
{
    /// <summary>
    /// Gets the costume async id generator.
    /// </summary>
    public Func<uint>? OnGenerateAsyncId { private get; init; }

    private readonly ConcurrentDictionary<uint, (TaskCompletionSource<TResult> Result, TData Data)>
        dictionary = new();

    private uint asyncId;

    /// <summary>
    /// Check if async task id is recorded.
    /// </summary>
    /// <param name="asyncTaskId">Async task id.</param>
    /// <returns>If async task id exists.</returns>
    public bool ContainsAsyncId(uint asyncTaskId) => this.dictionary.ContainsKey(asyncTaskId);

    /// <summary>
    /// Gets the task associated with the specified ID, if it exists.
    /// </summary>
    /// <param name="id">The ID of the task to retrieve.</param>
    /// <returns>The task associated with the specified ID, or null if no such task exists.</returns>
    public Task<TResult>? GetTaskById(uint id)
    {
        if (this.dictionary.TryGetValue(id, out var task))
        {
            return task.Result.Task;
        }

        return null;
    }

    /// <summary>
    /// Generate a async task with unique token id, with related data.
    /// </summary>
    /// <param name="data">Related data.</param>
    /// <returns>((TResult Result, TData Data), Token id) pair.</returns>
    public (Task<TResult> AsyncTaskTarget, uint TokenId) GenerateAsyncTask(TData data)
    {
        uint taskId = 0;
        if (this.OnGenerateAsyncId != null)
        {
            taskId = this.OnGenerateAsyncId.Invoke();
        }
        else
        {
            taskId = Interlocked.Increment(ref this.asyncId);
        }

        var source = new TaskCompletionSource<TResult>();
        this.dictionary[taskId] = (source, data);
        return (source.Task, taskId);
    }

    /// <summary>
    /// Generate a async task with unique token id, with timeout handler.
    /// </summary>
    /// <param name="data">Related data.</param>
    /// <param name="timeoutMilliseconds">Max milliseconds to wait.</param>
    /// <param name="timeOutHandler">What exception to throw. </param>
    /// <returns>(AsyncTask, Token id) pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeOutHandler"/> should not be null.</exception>
    public (Task<TResult> AsyncTaskTarget, uint TokenId) GenerateAsyncTask(
        TData data,
        int timeoutMilliseconds,
        Func<uint, Exception> timeOutHandler)
    {
        if (timeOutHandler == null)
        {
            throw new ArgumentNullException(nameof(timeOutHandler));
        }

        var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);
        var source = new TaskCompletionSource<TResult>();
        var taskId = Interlocked.Increment(ref this.asyncId);

        cancellationTokenSource.Token.Register(
            () =>
            {
                var res = this.dictionary.TryRemove(taskId, out _);
                while (!res)
                {
                    res = this.dictionary.TryRemove(taskId, out _);
                }

                source.TrySetException(timeOutHandler.Invoke(taskId));
            },
            false);

        this.dictionary[taskId] = (source, data);
        return (source.Task, taskId);
    }

    /// <summary>
    /// Get related data via async task id.
    /// </summary>
    /// <param name="asyncTaskId">Async task id.</param>
    /// <returns>Related data.</returns>
    public TData GetDataByAsyncTaskId(uint asyncTaskId)
    {
        var res = this.dictionary.TryGetValue(asyncTaskId, out var data);
        if (!res)
        {
            throw new Exception($"Invalid asyncTaskId {asyncTaskId}");
        }

        return data.Data;
    }

    /// <summary>
    /// Update recorded data.
    /// </summary>
    /// <param name="asyncTaskId">Async task id.</param>
    /// <param name="newData">New data.</param>
    public void UpdateDataByAsyncTaskId(uint asyncTaskId, TData newData)
    {
        var res = this.dictionary.TryGetValue(asyncTaskId, out var record);
        if (!res)
        {
            throw new Exception($"Invalid asyncTaskId {asyncTaskId}");
        }

        this.dictionary[asyncTaskId] = (record.Result, newData);
    }

    /// <summary>
    /// Resolve a task by token id.
    /// </summary>
    /// <param name="asyncId">Async task token id.</param>
    /// <param name="result">Result.</param>
    public void ResolveAsyncTask(uint asyncId, TResult result)
    {
        if (!this.dictionary.ContainsKey(asyncId))
        {
            return;
        }

        var res = this.dictionary.TryRemove(asyncId, out var source);
        while (!res)
        {
            res = this.dictionary.TryRemove(asyncId, out source);
        }

        source.Result !.TrySetResult(result);
    }
}