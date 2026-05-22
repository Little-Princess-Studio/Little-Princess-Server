// -----------------------------------------------------------------------
// <copyright file="Bus.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using System.Collections.Concurrent;
using System.Threading;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;

/// <summary>
/// Universal thread-safe message bus for handler the message queue.
/// <para>
/// Wakeup contract for consumers:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Pump"/> drains everything available right
/// now and returns immediately. Use when the consumer thread has its own
/// scheduling reason to tick (KCP needs a fixed 10ms cadence;
/// <see cref="LPS.Common.Rpc.Connection"/> pumps are driven externally by
/// Gate's per-tick SandBox).</description></item>
/// <item><description><see cref="WaitAndPump"/> blocks on an
/// <see cref="AutoResetEvent"/> until a producer calls
/// <see cref="AppendMessage"/>, then drains. Use when the consumer's only
/// job is processing messages - replaces the old <c>Pump() + Thread.Sleep(N)</c>
/// poll loop with event-driven wakeups. Producer-side signalling uses the
/// "wasEmpty" optimisation so back-to-back enqueues collapse to one
/// <see cref="AutoResetEvent.Set"/> call.</description></item>
/// <item><description><see cref="Shutdown"/> pokes the wait event so a
/// blocked consumer can re-check its stop flag and exit.</description></item>
/// </list>
/// </summary>
public class Bus
{
    private readonly ConcurrentQueue<Message> msgQueue = new();
    private readonly Dispatcher<(IMessage, Connection, uint)> dispatcher;

    // AutoResetEvent (not Semaphore) because we don't care about exact
    // message count - one wake is enough to drain everything pending.
    // Repeated Set() while in signaled state is a no-op, which is exactly
    // what we want for high-throughput producer bursts.
    private readonly AutoResetEvent signal = new(false);

    /// <summary>
    /// Initializes a new instance of the <see cref="Bus"/> class.
    /// </summary>
    /// <param name="dispatcher">Message dispatcher of the message queue.</param>
    public Bus(Dispatcher<(IMessage Message, Connection Connection, uint RpcId)> dispatcher)
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
        // "wasEmpty" optimisation: only Set() when the queue was empty
        // before this enqueue. During a burst (consumer still draining),
        // subsequent enqueues skip the Set() syscall entirely - the
        // consumer's drain loop will pick up the new items on the same
        // pass. Worst case is a benign extra Set() if the consumer just
        // finished its drain - AutoResetEvent collapses that to one wake.
        bool wasEmpty = this.msgQueue.IsEmpty;
        this.msgQueue.Enqueue(msg);
        if (wasEmpty)
        {
            this.signal.Set();
        }
    }

    /// <summary>
    /// Pump messages from message queue and handle them. Non-blocking -
    /// returns immediately if the queue is empty. Use this from consumers
    /// that tick on their own clock (KCP, externally-pumped TcpClient).
    /// </summary>
    public void Pump()
    {
        this.DrainAll();
    }

    /// <summary>
    /// Block until at least one message arrives (or <paramref name="timeoutMs"/>
    /// elapses, whichever comes first), then drain everything available.
    /// </summary>
    /// <param name="timeoutMs">
    /// Max wait. Set this short enough for the consumer to notice its stop
    /// flag promptly (e.g. 100ms). Returns <c>true</c> if at least one
    /// message was drained, <c>false</c> on timeout.
    /// </param>
    /// <returns><c>true</c> if work was processed, <c>false</c> on timeout.</returns>
    public bool WaitAndPump(int timeoutMs)
    {
        // Fast path: drain whatever already arrived before we slept.
        if (!this.msgQueue.IsEmpty)
        {
            this.DrainAll();
            return true;
        }

        if (!this.signal.WaitOne(timeoutMs))
        {
            return false;
        }

        this.DrainAll();
        return true;
    }

    /// <summary>
    /// Wake any consumer blocked in <see cref="WaitAndPump"/> so it can
    /// re-check its stop flag. Called from the owner's Stop() path.
    /// </summary>
    public void Shutdown()
    {
        this.signal.Set();
    }

    private void DrainAll()
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
            try
            {
                this.dispatcher.Dispatch(msg.Key, msg.Arg);
                succ = this.TryDeque(out msg);
            }
            catch (System.Exception e)
            {
                Logger.Error(e, "Error when dispatch message.");
                break;
            }
        }
        while (succ);
    }

    private bool TryDeque(out Message msg)
    {
        return this.msgQueue.TryDequeue(out msg!);
    }
}