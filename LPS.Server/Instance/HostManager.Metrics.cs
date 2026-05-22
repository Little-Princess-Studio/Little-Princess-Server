// -----------------------------------------------------------------------
// <copyright file="HostManager.Metrics.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;

/// <summary>
/// Lightweight in-process time-series collector. Every
/// <see cref="MetricsTickIntervalMs"/> the HostManager snapshots the live
/// instance-status counts and appends one sample to per-series ring
/// buffers (one buffer per series, fixed capacity = 5 minutes worth).
/// Exposed via <c>GET /api/web-manager/metrics-time-series</c> for the
/// MetricsPage line charts. No persistence - history vanishes on restart.
/// </summary>
public partial class HostManager
{
    private const int MetricsTickIntervalMs = 5000;
    private const int MetricsCapacity = 60; // 60 * 5s = 5 minutes

    private readonly RingBuffer aliveGatesSeries = new(MetricsCapacity);
    private readonly RingBuffer aliveServersSeries = new(MetricsCapacity);
    private readonly RingBuffer aliveServiceMgrSeries = new(MetricsCapacity);
    private readonly RingBuffer aliveServicesSeries = new(MetricsCapacity);
    private readonly RingBuffer pingSuccessRateSeries = new(MetricsCapacity);

    private Timer? metricsTimer;

    private void StartMetricsCollector()
    {
        Logger.Info($"[HostManager.Metrics] starting collector, tick={MetricsTickIntervalMs}ms, capacity={MetricsCapacity}");
        this.metricsTimer = new Timer(_ => this.SampleOnce(), null, 0, MetricsTickIntervalMs);
    }

    private void StopMetricsCollector()
    {
        this.metricsTimer?.Dispose();
        this.metricsTimer = null;
    }

    /// <summary>
    /// Replies with every ring buffer as <c>{name: [{t,v}, ...]}</c>. The
    /// frontend converts timestamps to local-time labels on the X axis.
    /// </summary>
    [WebMgrHandler("getMetricsTimeSeries.toHostMgr")]
    private JToken HandleGetMetricsTimeSeries(JToken body)
    {
        _ = body;
        return new JObject
        {
            ["series"] = new JObject
            {
                ["aliveGates"] = this.aliveGatesSeries.ToJson(),
                ["aliveServers"] = this.aliveServersSeries.ToJson(),
                ["aliveServiceManagers"] = this.aliveServiceMgrSeries.ToJson(),
                ["aliveServices"] = this.aliveServicesSeries.ToJson(),
                ["pingSuccessRate"] = this.pingSuccessRateSeries.ToJson(),
            },
            ["intervalMs"] = MetricsTickIntervalMs,
            ["capacity"] = MetricsCapacity,
        };
    }

    private void SampleOnce()
    {
        try
        {
            var snapshot = this.instanceStatusManager.Snapshot();
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            int gates = 0, servers = 0, svcMgr = 0, services = 0, total = 0, alive = 0;
            foreach (var s in snapshot)
            {
                total++;
                if ((int)s.Status == 1)
                {
                    alive++;
                    switch (s.InstanceType)
                    {
                        case InstanceType.Gate: gates++; break;
                        case InstanceType.Server: servers++; break;
                        case InstanceType.ServiceManager: svcMgr++; break;
                        case InstanceType.Service: services++; break;
                    }
                }
            }

            this.aliveGatesSeries.Push(ts, gates);
            this.aliveServersSeries.Push(ts, servers);
            this.aliveServiceMgrSeries.Push(ts, svcMgr);
            this.aliveServicesSeries.Push(ts, services);
            this.pingSuccessRateSeries.Push(ts, total == 0 ? 100.0 : (alive * 100.0 / total));
        }
        catch (Exception e)
        {
            Logger.Error(e, "[HostManager.Metrics] sample failed");
        }
    }

    /// <summary>
    /// Tiny fixed-capacity ring buffer of (timestamp, value) samples. Thread-safe
    /// via a single lock since both the sampler timer and the WebMgr dispatch
    /// thread can touch it. Capacity is small (~60), copy-on-read is fine.
    /// </summary>
    private sealed class RingBuffer
    {
        private readonly (long T, double V)[] buf;
        private readonly object gate = new();
        private int head;
        private int count;

        public RingBuffer(int capacity)
        {
            this.buf = new (long, double)[capacity];
        }

        public void Push(long t, double v)
        {
            lock (this.gate)
            {
                this.buf[this.head] = (t, v);
                this.head = (this.head + 1) % this.buf.Length;
                if (this.count < this.buf.Length)
                {
                    this.count++;
                }
            }
        }

        public JArray ToJson()
        {
            (long T, double V)[] snapshot;
            int snapshotCount;
            int snapshotHead;
            lock (this.gate)
            {
                snapshot = ((long, double)[])this.buf.Clone();
                snapshotCount = this.count;
                snapshotHead = this.head;
            }

            // Walk oldest -> newest. When buffer not yet full, oldest is index 0.
            // Otherwise it's `head` (the next write slot = oldest valid).
            var arr = new JArray();
            var start = snapshotCount < snapshot.Length ? 0 : snapshotHead;
            for (int i = 0; i < snapshotCount; i++)
            {
                var (t, v) = snapshot[(start + i) % snapshot.Length];
                arr.Add(new JObject { ["t"] = t, ["v"] = v });
            }

            return arr;
        }
    }
}
