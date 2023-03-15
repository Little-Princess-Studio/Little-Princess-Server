// -----------------------------------------------------------------------
// <copyright file="SandBox.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Ipc
{
    using LPS.Common.Core.Debug;

    /// <summary>
    /// Every thread works in a isolate sandbox.
    /// </summary>
    public class SandBox
    {
        private Thread? thread;

        /// <summary>
        /// Gets the thread id of the sandbox.
        /// </summary>
        /// <exception cref="Exception">Throw exception if sandbox is empty.</exception>
        public int ThreadId => this.thread?.ManagedThreadId ?? throw new Exception("SandBox is empty.");

        private object? action;

        private bool isAsyncAction;

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
                isAsyncAction = false,
            };

            return sandbox;
        }

        /// <summary>
        /// Create a sandbox with async handler.
        /// </summary>
        /// <param name="action">Async handler.</param>
        /// <returns>Sandbox object.</returns>
        public static SandBox Create(Func<Task> action)
        {
            var sandbox = new SandBox
            {
                action = action,
                isAsyncAction = true,
            };

            return sandbox;
        }

        /// <summary>
        /// Run the sandbox.
        /// </summary>
        public void Run()
        {
            this.thread = new Thread(() =>
            {
                try
                {
                    if (this.isAsyncAction)
                    {
                        var promise = (this.action as Func<Task>)!();
                        promise.Wait();
                    }
                    else
                    {
                        (this.action as Action)!();
                    }
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

        /// <summary>
        /// Wait until the sandbox exits.
        /// </summary>
        public void WaitForExit()
        {
            this.thread!.Join();
        }

        /// <summary>
        /// Force to interrupt the thread.
        /// </summary>
        public void Interrupt()
        {
            this.thread!.Interrupt();
        }
    }
}