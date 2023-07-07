// -----------------------------------------------------------------------
// <copyright file="TimerManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

/// <summary>
/// Manages a collection of timers that can be used to schedule and execute code at specific intervals.
/// </summary>
public class TimerManager
{
    private readonly List<Timer> timers = new();
    private int nextTimerId = 0;

    /// <summary>
    /// Updates all timers and removes any that have expired.
    /// </summary>
    /// <param name="duration">The duration since the last update.</param>
    public void Update(uint duration)
    {
        // Update all timers and remove any that have expired.
        for (int i = this.timers.Count - 1; i >= 0; i--)
        {
            Timer timer = this.timers[i];
            timer.TimeLeft -= duration;

            if (timer.TimeLeft <= 0)
            {
                timer.Action();
                if (timer.IsRepeat)
                {
                    timer.TimeLeft = timer.Interval;
                }
                else
                {
                    this.timers.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Schedules a one-time timer that will execute the specified action after the specified number of milliseconds.
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds to wait before executing the action.</param>
    /// <param name="action">The action to execute when the timer expires.</param>
    /// <returns>The ID of the scheduled timer.</returns>
    public int ScheduleOnetime(uint milliseconds, Action action)
    {
        Timer timer = new(this.nextTimerId++, milliseconds, action, false);
        this.timers.Add(timer);
        return timer.Id;
    }

    /// <summary>
    /// Schedules a repeating timer that will execute the specified action every specified number of milliseconds.
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds to wait before executing the action each time.</param>
    /// <param name="action">The action to execute when the timer expires.</param>
    /// <returns>The ID of the scheduled timer.</returns>
    public int ScheduleRepeat(uint milliseconds, Action action)
    {
        Timer timer = new(this.nextTimerId++, milliseconds, action, true);
        this.timers.Add(timer);
        return timer.Id;
    }

    /// <summary>
    /// Cancels the timer with the specified ID.
    /// </summary>
    /// <param name="timerId">The ID of the timer to cancel.</param>
    /// <returns>The ID of the canceled timer, or -1 if no timer was found with the specified ID.</returns>
    public int CancelTimer(int timerId)
    {
        for (int i = 0; i < this.timers.Count; i++)
        {
            if (this.timers[i].Id == timerId)
            {
                this.timers.RemoveAt(i);
                return timerId;
            }
        }

        return -1;
    }

    private class Timer
    {
        public int Id { get; }

        public uint Interval { get; }

        public Action Action { get; }

        public bool IsRepeat { get; }

        public uint TimeLeft { get; set; }

        public Timer(int id, uint interval, Action action, bool isRepeat)
        {
            this.Id = id;
            this.Interval = interval;
            this.Action = action;
            this.IsRepeat = isRepeat;
            this.TimeLeft = interval;
        }
    }
}