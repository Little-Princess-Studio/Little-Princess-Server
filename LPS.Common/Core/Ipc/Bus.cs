// -----------------------------------------------------------------------
// <copyright file="Bus.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Ipc
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Universal thread-safe message bus for handler the message queue.
    /// </summary>
    public class Bus
    {
        private readonly ConcurrentQueue<Message> msgQueue = new();
        private readonly Dispatcher dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="dispatcher">Message dispatcher of the message queue.</param>
        public Bus(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        private bool Empty => this.msgQueue.IsEmpty;

        /// <summary>
        /// Append a message to messag queue.
        /// </summary>
        /// <param name="msg">Message.</param>
        public void AppendMessage(Message msg)
        {
            this.msgQueue.Enqueue(msg);
        }

        /// <summary>
        /// Pump messages from message queue and handle them.
        /// </summary>
        public void Pump()
        {
            if (this.Empty)
            {
                return;
            }

            bool succ = this.TryDeque(out var msg);

            if (!succ)
            {
                return;
            }

            do
            {
                this.dispatcher.Dispatch(msg.Key, msg.Arg);
                succ = this.TryDeque(out msg);
            }
            while (succ);
        }

        private bool TryDeque(out Message msg)
        {
            return this.msgQueue.TryDequeue(out msg!);
        }
    }
}