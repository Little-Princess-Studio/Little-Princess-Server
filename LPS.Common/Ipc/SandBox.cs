// -----------------------------------------------------------------------
// <copyright file="SandBox.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using LPS.Common.Debug;

/// <summary>
/// Every thread works in a isolate sandbox.
/// </summary>
public class SandBox
{
    private Thread? thread;
    private Task? asyncTask;

    /// <summary>
    /// Gets the thread id of the sandbox.
    /// </summary>
    /// <exception cref="Exception">Throw exception if sandbox is empty.</exception>
    public int ThreadId => this.thread?.ManagedThreadId ?? throw new Exception("SandBox is empty.");

    private object? action;
    private bool isAsync;

    private SandBox()
    {
    }

    /// <summary>
    /// Create a sandbox.
    /// </summary>
    /// <param name="action">Content.</param>
    /// <returns>Sandbox object.</returns>
    public static SandBox Create(Action action)
    {
        var sandbox = new SandBox
        {
            action = action,
            isAsync = false,
        };

        return sandbox;
    }

    /// <summary>
    /// Create a sandbox with long running task.
    /// </summary>
    /// <param name="task">Content.</param>
    /// <returns>Sandbox object.</returns>
    public static SandBox Create(Func<Task> task)
    {
        var sandbox = new SandBox
        {
            action = task,
            isAsync = true,
        };

        return sandbox;
    }

    /// <summary>
    /// Run the sandbox.
    /// </summary>
    public void Run()
    {
        if (this.isAsync)
        {
            // directly run the async action
            this.asyncTask = (this.action as Func<Task>)!();
            this.asyncTask.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Logger.Error(t.Exception);
                }
            });
        }
        else
        {
            this.thread = new Thread(() =>
            {
                try
                {
                    (this.action as Action)!();
                }
                catch (ThreadInterruptedException ex)
                {
                    Logger.Error(ex, "Sandbox thread interupted.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Exception happend in SandBox");
                }
            });
            this.thread.Start();
        }
    }

    /// <summary>
    /// Wait until the sandbox exits.
    /// </summary>
    public void WaitForExit()
    {
        if (!this.isAsync)
        {
            this.thread!.Join();
        }
        else
        {
            this.asyncTask!.Wait();
        }
    }
}