// -----------------------------------------------------------------------
// <copyright file="ProcessExitCoordinator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using System.Threading.Tasks;
using LPS.Common.Debug;

/// <summary>
/// Drains a subprocess and exits the process cleanly so
/// <see cref="StartupManager"/>'s auto-restart logic (which only respawns on
/// non-zero exit codes) treats the shutdown as intentional.
/// </summary>
/// <remarks>
/// <para>
/// The graceful shutdown contract used cluster-wide:
/// </para>
/// <list type="number">
/// <item><description>WebManager sends a <c>ShutdownInstance</c> HostCommand
/// (with an optional <c>ShutdownTimeoutMs</c>) targeted at one instance.</description></item>
/// <item><description>The instance's HostCommand handler hands control to
/// <see cref="Schedule(string, Action, int)"/>, which:
///   <list type="bullet">
///     <item><description>fires a watchdog that calls <see cref="Environment.Exit(int)"/>
///       with exit code 0 after <paramref name="timeoutMs"/> in case the drain hangs;</description></item>
///     <item><description>runs the per-instance drain action on a background task and,
///       on completion, calls <see cref="Environment.Exit(int)"/> with code 0.</description></item>
///   </list>
/// </description></item>
/// <item><description>Exit code 0 makes <c>StartupManager.StartSubProcess</c>'s
/// <c>Process.Exited</c> handler take the "expected" branch and skip respawn.</description></item>
/// </list>
/// <para>
/// Safe to invoke multiple times - the first call wins; subsequent calls
/// no-op so duplicate WebManager clicks do not double-schedule Exit.
/// </para>
/// </remarks>
public static class ProcessExitCoordinator
{
    /// <summary>Default drain budget when WebManager passes 0 / unspecified.</summary>
    public const int DefaultTimeoutMs = 10000;

    private static readonly object Gate = new();
    private static bool scheduled;

    /// <summary>
    /// Schedule a graceful drain followed by <see cref="Environment.Exit(int)"/>
    /// with exit code 0. Idempotent across repeated invocations.
    /// </summary>
    /// <param name="instanceName">Logging tag - typically the instance's <c>Name</c>.</param>
    /// <param name="drain">Per-instance teardown closure. Should be synchronous and
    /// must not block forever (the watchdog will force-exit if it does).</param>
    /// <param name="timeoutMs">Wall-clock budget for the drain. Zero or negative
    /// falls back to <see cref="DefaultTimeoutMs"/>.</param>
    public static void Schedule(string instanceName, Action drain, int timeoutMs)
    {
        lock (Gate)
        {
            if (scheduled)
            {
                Logger.Info($"[ProcessExitCoordinator] Shutdown already scheduled for {instanceName}, ignoring duplicate request.");
                return;
            }

            scheduled = true;
        }

        var effectiveTimeout = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;
        Logger.Info($"[ProcessExitCoordinator] Graceful shutdown requested for {instanceName} with {effectiveTimeout}ms budget.");

        // Watchdog: hard-exit if drain hangs past the budget. Runs on a
        // background thread so a deadlocked drain (e.g. a Wait() that never
        // returns) cannot keep the process pinned.
        _ = Task.Run(async () =>
        {
            await Task.Delay(effectiveTimeout).ConfigureAwait(false);
            Logger.Warn($"[ProcessExitCoordinator] Shutdown timeout reached for {instanceName}, forcing exit(0).");
            Environment.Exit(0);
        });

        // Drain: run teardown then exit cleanly. The Environment.Exit(0) here
        // is the happy path; the watchdog is the safety net.
        _ = Task.Run(() =>
        {
            try
            {
                drain();
                Logger.Info($"[ProcessExitCoordinator] Drain complete for {instanceName}, calling Exit(0).");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[ProcessExitCoordinator] Drain failed for {instanceName}, exiting anyway.");
            }

            Environment.Exit(0);
        });
    }
}
