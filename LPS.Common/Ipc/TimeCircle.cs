// -----------------------------------------------------------------------
// <copyright file="TimeCircle.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using System.Collections.Concurrent;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

/// <summary>
/// Time circle for property sync.
/// </summary>
public class TimeCircle
{
    private readonly int timeInterval;

    // private readonly int totalMillisecondsPerCircle_;
    private readonly ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)> waitingMessageQueue;
    private readonly TimeCircleSlot[] slots;
    private readonly int slotsPerCircle;

    private int slotIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeCircle"/> class.
    /// </summary>
    /// <param name="timeIntervalByMillisecond">Time circle tick time interval.</param>
    /// <param name="totalMillisecondsPerCircle">Total time per time circle.</param>
    public TimeCircle(int timeIntervalByMillisecond, int totalMillisecondsPerCircle)
    {
        if (1000 % timeIntervalByMillisecond != 0)
        {
            throw new Exception("Error time interval for time circle.");
        }

        this.timeInterval = timeIntervalByMillisecond;

        // totalMillisecondsPerCircle_ = totalMillisecondsPerCircle;
        this.waitingMessageQueue = new ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)>();
        this.slotsPerCircle = totalMillisecondsPerCircle / this.timeInterval;
        this.slots = new TimeCircleSlot[this.slotsPerCircle];
        for (int i = 0; i < this.slotsPerCircle; ++i)
        {
            this.slots[i] = new TimeCircleSlot();
        }
    }

    /// <summary>
    /// Start the time circle.
    /// </summary>
    public void Start()
    {
        this.slotIndex = 0;
    }

    /// <summary>
    /// Add property sync message to time circle.
    /// </summary>
    /// <param name="msg">Sync message.</param>
    /// <param name="delayTimeByMillisecond">Delay dispatch time.</param>
    /// <param name="keepOrder">If the message should keep order.</param>
    public void AddPropertySyncMessage(RpcPropertySyncMessage msg, uint delayTimeByMillisecond, bool keepOrder)
    {
        // can arrange directly
        if (delayTimeByMillisecond <= this.slotsPerCircle * this.timeInterval)
        {
            var arrangeSlot = (this.slotIndex +
                               (uint)Math.Floor(delayTimeByMillisecond / (decimal)this.timeInterval))
                              % this.slotsPerCircle;
            if (keepOrder)
            {
                this.slots[arrangeSlot].AddSyncMessageKeepOrder(msg);
            }
            else
            {
                this.slots[arrangeSlot].AddSyncMessageNoKeepOrder(msg);
            }
        }

        // arrange to waiting queue
        else
        {
            var arrangeTime = ((uint)this.slotIndex * (uint)this.timeInterval) + delayTimeByMillisecond;
            this.waitingMessageQueue.Enqueue((keepOrder, arrangeTime, msg));
        }
    }

    /// <summary>
    /// Fill a slot.
    /// </summary>
    /// <param name="slot">Slot.</param>
    /// <param name="slotIndex">Slot index.</param>
    public void FillSlot(TimeCircleSlot slot, int slotIndex)
    {
        slot.Clear();
        var targetEndTime = (uint)(slotIndex + 1) * this.timeInterval;
        do
        {
            if (this.waitingMessageQueue.IsEmpty)
            {
                break;
            }

            var res = this.waitingMessageQueue.TryPeek(out var candidate);
            if (!res)
            {
                break;
            }

            var (_, msgDispatchTime, _) = candidate;
            if (msgDispatchTime <= targetEndTime)
            {
                this.waitingMessageQueue.TryDequeue(out candidate);
                var (keepOrder, _, msg) = candidate;
                if (keepOrder)
                {
                    slot.AddSyncMessageKeepOrder(msg);
                }
                else
                {
                    slot.AddSyncMessageNoKeepOrder(msg);
                }
            }
            else
            {
                break;
            }
        }
        while (true);
    }

    /// <summary>
    /// Example:
    /// dispatch 0 [0...50] -> fill 60 to 0
    /// dispatch 1 [51...100] -> fill 61 to 1
    /// dispatch n [101 ... 150] -> fill n + 60 to n.
    /// </summary>
    /// <param name="duration">Time delta since last tick.</param>
    /// <param name="dispatch">Property sync dispatch handler.</param>
    public void Tick(uint duration, Action<PropertySyncCommandList> dispatch)
    {
        // move forward
        var moveStep = duration / this.timeInterval;

        for (int i = 0; i < moveStep; i++)
        {
            var slotCircleIndex = this.slotIndex % this.slotsPerCircle;
            var slot = this.slots[slotCircleIndex];

            slot.Dispatch(dispatch);
            this.FillSlot(slot, this.slotIndex + this.slotsPerCircle);
            ++this.slotIndex;
        }
    }
}