// -----------------------------------------------------------------------
// <copyright file="KcpServerNetwork.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using kcp2k;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// kcp2k-backed server transport, parallel to <see cref="TcpServer"/>. Same
/// dispatch surface (<c>RegisterMessageHandler</c>) so Gate's existing
/// <see cref="LPS.Common.Rpc.InnerMessages.PackageType"/> handlers fire
/// regardless of which transport delivered the bytes.
/// <para>
/// Design notes:
/// </para>
/// <list type="bullet">
/// <item><description>kcp2k is single-threaded cooperative. We pin its
/// <c>Tick()</c> loop to one SandBox running at <c>KcpConfig.Interval</c>
/// (default 10 ms) - exactly the model the upstream README documents.</description></item>
/// <item><description>kcp2k delivers <c>OnData(connId, ArraySegment, channel)</c>
/// where the byte segment is exactly one application message - no stream
/// reassembly required. We hand it to <see cref="PackageHelper.GetPackage"/>
/// directly and skip <see cref="MessageBuffer"/> entirely.</description></item>
/// <item><description>Per-client wrapping uses
/// <see cref="KcpConnection.ForServer"/> so callers see a <see cref="Connection"/>
/// indistinguishable from a TCP one (apart from runtime type).</description></item>
/// </list>
/// </summary>
internal class KcpServerNetwork
{
    /// <summary>Gets the IP this transport listens on.</summary>
    public string Ip { get; }

    /// <summary>Gets the UDP port this transport listens on.</summary>
    public int Port { get; }

    /// <summary>Gets a value indicating whether the transport has been stopped.</summary>
    public bool Stopped => this.stopFlag;

    /// <summary>Gets the kcp2k tick interval in milliseconds.</summary>
    public uint Interval { get; }

    /// <summary>Gets the live <see cref="Connection"/> set (one per kcp2k peer).</summary>
    public Connection[] AllConnections
    {
        get
        {
            lock (this.connectionsLock)
            {
                return this.connections.Values.ToArray();
            }
        }
    }

    // EventKind tags drained from kcp2k callbacks (which fire inside Tick()
    // on the tick thread). We re-emit them into a queue that the bus pump
    // drains so handler invocation runs on LPS's existing dispatch thread,
    // not kcp2k's tick thread.
    private const byte EventConnected = 1;
    private const byte EventData = 2;
    private const byte EventDisconnected = 3;

    private readonly Dispatcher<(IMessage, Connection, uint)> msgDispatcher;
    private readonly Bus bus;
    private readonly SandBox sandboxTick;
    private readonly KcpServer kcpServer;
    private readonly KcpConfig config;
    private readonly Dictionary<int, KcpConnection> connections = new();
    private readonly object connectionsLock = new();
    private readonly ConcurrentQueue<(int ConnId, byte EventKind, ArraySegment<byte> Payload)> eventQueue = new();

    private bool stopFlag;
    private uint serverPackageId;

    /// <summary>
    /// Initializes a new instance of the <see cref="KcpServerNetwork"/> class.
    /// </summary>
    /// <param name="ip">Listen IP (kcp2k binds to <c>0.0.0.0</c> internally;
    /// the value is kept for parity with <see cref="TcpServer"/>).</param>
    /// <param name="port">UDP port.</param>
    /// <param name="config">Optional kcp2k config; defaults to the
    /// turbo-mode preset used in <see cref="DefaultConfig"/>.</param>
    public KcpServerNetwork(string ip, int port, KcpConfig? config = null)
    {
        this.Ip = ip;
        this.Port = port;
        this.config = config ?? DefaultConfig();
        this.Interval = this.config.Interval;

        this.msgDispatcher = new Dispatcher<(IMessage, Connection, uint)>();
        this.bus = new Bus(this.msgDispatcher);

        this.kcpServer = new KcpServer(
            OnConnected: this.OnKcpConnected,
            OnData: this.OnKcpData,
            OnDisconnected: this.OnKcpDisconnected,
            OnError: this.OnKcpError,
            config: this.config);

        this.sandboxTick = SandBox.Create(this.TickLoop);
    }

    /// <summary>Gets the kcp2k turbo-mode preset suitable for game traffic.</summary>
    /// <returns>A <see cref="KcpConfig"/> tuned for low latency.</returns>
    public static KcpConfig DefaultConfig() => new(
        DualMode: false,
        NoDelay: true,
        Interval: 10,
        Timeout: 10000,
        FastResend: 2,
        CongestionWindow: false,
        SendWindowSize: Kcp.WND_SND * 4,
        ReceiveWindowSize: Kcp.WND_RCV * 4);

    /// <summary>Start listening for KCP peers.</summary>
    public void Run()
    {
        this.stopFlag = false;
        this.kcpServer.Start((ushort)this.Port);
        Logger.Info($"[KcpServerNetwork] Listening on udp://{this.Ip}:{this.Port}");
        this.sandboxTick.Run();
    }

    /// <summary>Block until the tick loop exits.</summary>
    public void WaitForExit() => this.sandboxTick.WaitForExit();

    /// <summary>Stop accepting new peers and tear down active ones.</summary>
    public void Stop()
    {
        if (this.stopFlag)
        {
            return;
        }

        Logger.Info("[KcpServerNetwork] Stopping.");
        this.stopFlag = true;

        try
        {
            this.kcpServer.Stop();
        }
        catch (Exception e)
        {
            Logger.Warn($"[KcpServerNetwork] kcp2k Stop threw: {e.Message}");
        }
    }

    /// <summary>
    /// Enqueue a message to a specific KCP peer. Mirrors
    /// <see cref="TcpServer.Send"/>; the actual write goes through
    /// <see cref="KcpConnection.Send"/> which dispatches into kcp2k.
    /// </summary>
    /// <param name="msg">Protobuf message.</param>
    /// <param name="conn">Target connection.</param>
    public void Send(IMessage msg, Connection conn)
    {
        if (conn.Status != ConnectStatus.Connected)
        {
            return;
        }

        try
        {
            var id = this.serverPackageId++;
            var pkg = PackageHelper.FromProtoBuf(msg, id);
            conn.Send(pkg.ToBytes());
        }
        catch (Exception e)
        {
            Logger.Error(e, "[KcpServerNetwork] Send failed.");
        }
    }

    /// <inheritdoc cref="TcpServer.RegisterMessageHandler"/>
    public void RegisterMessageHandler(IComparable key, Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
        => this.msgDispatcher.Register(key, callback);

    /// <inheritdoc cref="TcpServer.UnregisterMessageHandler"/>
    public void UnregisterMessageHandler(IComparable key, Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
        => this.msgDispatcher.Unregister(key, callback);

    // kcp2k callbacks fire on the tick thread. We re-queue work into
    // eventQueue so the bus pump (which runs the same dispatch chain
    // TcpServer uses) sees events at a predictable point.
    private void OnKcpConnected(int connId)
    {
        Logger.Info($"[KcpServerNetwork] OnConnected {connId}");
        this.eventQueue.Enqueue((connId, EventConnected, default));
    }

    private void OnKcpData(int connId, ArraySegment<byte> data, KcpChannel channel)
    {
        // Copy into our own buffer - kcp2k recycles ArraySegments after the
        // callback returns. The eventQueue is consumed on a different
        // thread (the tick SandBox), so we cannot retain the segment.
        var copy = new byte[data.Count];
        Buffer.BlockCopy(data.Array!, data.Offset, copy, 0, data.Count);
        this.eventQueue.Enqueue((connId, EventData, new ArraySegment<byte>(copy)));
    }

    private void OnKcpDisconnected(int connId)
    {
        Logger.Info($"[KcpServerNetwork] OnDisconnected {connId}");
        this.eventQueue.Enqueue((connId, EventDisconnected, default));
    }

    private void OnKcpError(int connId, ErrorCode code, string reason)
    {
        Logger.Warn($"[KcpServerNetwork] connId={connId} kcp error {code}: {reason}");
    }

    private void TickLoop()
    {
        Logger.Debug($"[KcpServerNetwork] Tick loop start (interval={this.Interval}ms).");

        while (!this.stopFlag)
        {
            try
            {
                this.kcpServer.Tick();
                this.DrainEventQueue();
                this.bus.Pump();
            }
            catch (Exception e)
            {
                Logger.Error(e, "[KcpServerNetwork] tick iteration failed.");
            }

            Thread.Sleep((int)this.Interval);
        }

        Logger.Info("[KcpServerNetwork] Tick loop exit.");
    }

    private void DrainEventQueue()
    {
        while (this.eventQueue.TryDequeue(out var evt))
        {
            switch (evt.EventKind)
            {
                case EventConnected:
                    this.HandleConnectEvent(evt.ConnId);
                    break;
                case EventData:
                    this.HandleDataEvent(evt.ConnId, evt.Payload);
                    break;
                case EventDisconnected:
                    this.HandleDisconnectEvent(evt.ConnId);
                    break;
            }
        }
    }

    private void HandleConnectEvent(int connId)
    {
        var conn = KcpConnection.ForServer(this.kcpServer, connId);
        conn.Connect();
        lock (this.connectionsLock)
        {
            this.connections[connId] = conn;
        }
    }

    private void HandleDataEvent(int connId, ArraySegment<byte> payload)
    {
        KcpConnection? conn;
        lock (this.connectionsLock)
        {
            this.connections.TryGetValue(connId, out conn);
        }

        if (conn is null)
        {
            Logger.Warn($"[KcpServerNetwork] data for unknown connId {connId}, dropping.");
            return;
        }

        try
        {
            // KCP delivers one full message per callback. No reassembly.
            var pkg = PackageHelper.GetPackage(payload.AsSpan());
            var type = (PackageType)pkg.Header.Type;
            var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
            var arg = (pb, (Connection)conn, pkg.Header.ID);
            this.bus.AppendMessage(new Message(type, arg));
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[KcpServerNetwork] parse failed for connId {connId}.");
        }
    }

    private void HandleDisconnectEvent(int connId)
    {
        KcpConnection? conn;
        lock (this.connectionsLock)
        {
            if (this.connections.TryGetValue(connId, out conn))
            {
                this.connections.Remove(connId);
            }
        }

        conn?.Disconnect();
    }
}
