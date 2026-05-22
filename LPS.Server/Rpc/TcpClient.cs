// -----------------------------------------------------------------------
// <copyright file="TcpClient.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// TcpClient is the common client proxy used inside the host to connect to TcpServer.
/// <para>
/// Lifecycle: <c>OnInit</c> fires once at <see cref="Run"/>. The IoHandler runs a
/// state machine that establishes the initial connection (firing <c>OnConnected</c>
/// exactly once on success) and then loops on the message-receive path. When the
/// socket dies mid-run, the client emits <c>OnDisconnected</c>, waits a backoff
/// interval, and reconnects. Each successful reconnect fires <c>OnReconnected</c>
/// so call sites can re-do their non-idempotent handshakes (e.g. send
/// <c>Control{Restart}</c> to HostManager). <c>OnDispose</c> fires once at the
/// terminal stop.
/// </para>
/// <para>
/// Reconnect uses exponential backoff capped at 30 s (1, 2, 4, 8, 16, 30, 30…)
/// with ±20% jitter. It never gives up while <see cref="stopFlag"/> is false; if
/// the peer process is genuinely dead the supervisor will respawn it. The
/// per-connection RpcId space is shared via the base <see cref="Connection"/>'s
/// counter, but our outbound message id counter <see cref="counterOfId"/> is
/// monotonic across reconnects to avoid collisions with peer-side buffers from
/// the previous socket.
/// </para>
/// </summary>
internal class TcpClient // : IClient
{
    /// <summary>
    /// Gets the socket of the client. Null between disconnects; replaced on every reconnect.
    /// </summary>
    public Socket? Socket { get; private set; }

    /// <summary>
    /// Gets the callback when init client. Fires once at <see cref="Run"/>.
    /// </summary>
    public Action<TcpClient>? OnInit { private get; init; }

    /// <summary>
    /// Gets the callback when dispose the client. Fires once at terminal stop
    /// (after <see cref="Stop"/> or when initial-connect exhausts the retry budget).
    /// Does NOT fire on transient disconnects - use <see cref="OnDisconnected"/>
    /// for those.
    /// </summary>
    public Action<TcpClient>? OnDispose { private get; init; }

    /// <summary>
    /// Gets the callback when first connected to server. Fires exactly once on
    /// successful initial connect. Subsequent reconnects fire <see cref="OnReconnected"/>
    /// instead so call sites can distinguish startup-only registration from
    /// resume-only restart handshakes.
    /// </summary>
    public Action<TcpClient>? OnConnected { private get; init; }

    /// <summary>
    /// Gets the callback when the socket dies mid-run (after at least one
    /// successful connect). Fires before reconnect attempts begin. Useful for
    /// surfacing connection loss to higher layers (e.g. mark MailBox as
    /// "in-flight RPCs may have leaked").
    /// </summary>
    public Action<TcpClient>? OnDisconnected { private get; init; }

    /// <summary>
    /// Gets the callback when the client re-establishes a connection after
    /// losing it. Fires on every successful reconnect (NOT on the first connect).
    /// Call sites use this to re-send <c>Control{Restart}</c> handshakes to
    /// HostManager which already supports restart-registration via
    /// HostManager.Register.cs:223 RestartInstance.
    /// </summary>
    public Action<TcpClient>? OnReconnected { private get; init; }

    /// <summary>
    /// Gets or sets the mailbox of the client.
    /// </summary>
    public MailBox MailBox { get; set; }

    /// <summary>
    /// Gets a value indicating whether the underlying socket is currently
    /// connected. False during the reconnect-backoff window.
    /// </summary>
    public bool IsConnected => this.Socket is not null && this.Socket.Connected;

    /// <summary>
    /// Gets the count of consecutive failed reconnect attempts since the last
    /// successful connect. Resets to 0 on every successful (re)connect. Useful
    /// for observability/logging.
    /// </summary>
    public int ReconnectAttempt { get; private set; }

    /// <summary>
    /// Gets the remote port.
    /// </summary>
    public int TargetPort => this.targetPort;

    /// <summary>
    /// Gets the remote IP.
    /// </summary>
    public string TargetIp => this.targetIp;

    private const int ConnectRetryMaxTimes = 10;
    private const int ReconnectBackoffMaxMs = 30_000;
    private const int ReconnectBackoffBaseMs = 1_000;

    private static readonly Random JitterRng = new();

    private readonly SandBox sandboxIo;
    private readonly Bus bus;
    private readonly Dispatcher<(IMessage Message, Connection Connection, uint RpcId)> msgDispatcher;
    private readonly string targetIp;
    private readonly int targetPort;

    // private readonly TokenSequence<uint> tokenSequence = new();
    private readonly ConcurrentQueue<(TcpClient, IMessage, bool)> sendQueue;
    private readonly SandBox clientsSendQueueSandBox;

    // Wakeable backoff: Stop() cancels this so a reconnecting client exits
    // promptly instead of sleeping out the full backoff window.
    private readonly CancellationTokenSource backoffCts = new();

    private bool stopFlag;
    private uint counterOfId;
    private SocketConnection? connection;
    private bool firstConnectDone;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClient"/> class.
    /// </summary>
    /// <param name="targetIp">Remote IP.</param>
    /// <param name="targetPort">Remote port.</param>
    /// <param name="sendQueue">Queue to receive and send message to remote server.</param>
    public TcpClient(
        string targetIp,
        int targetPort,
        ConcurrentQueue<(TcpClient TcpClient, IMessage Message, bool IsReentry)> sendQueue)
    {
        this.targetIp = targetIp;
        this.targetPort = targetPort;
        this.sendQueue = sendQueue;

        this.msgDispatcher = new Dispatcher<(IMessage Message, Connection Connection, uint RpcId)>();
        this.bus = new Bus(this.msgDispatcher);

        this.sandboxIo = SandBox.Create(this.IoHandler);

        // TODO: use a common sandbox to handle all send queue.
        this.clientsSendQueueSandBox = SandBox.Create(this.SendQueueMessageHandler);
    }

    /// <summary>
    /// Send message to server. If the client is currently disconnected the
    /// message is dropped at the dequeue site (see SendQueueMessageHandler) -
    /// callers awaiting on the corresponding RPC will surface as a timeout or
    /// RpcException. There is intentionally no outbox/replay buffer; the
    /// send-queue redesign (a separate TODO) would add that.
    /// </summary>
    /// <param name="msg">Message to send.</param>
    /// <param name="reentry">If the message is reentry-able.</param>
    public void Send(IMessage msg, bool reentry = true)
    {
        try
        {
            this.sendQueue.Enqueue((this, msg, reentry));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Send Error.");
        }
    }

    /// <summary>
    /// Pump message to send.
    /// </summary>
    public void Pump() => this.bus.Pump();

    /// <summary>
    /// Start client.
    /// </summary>
    public void Run()
    {
        this.stopFlag = false;
        this.clientsSendQueueSandBox.Run();
        Logger.Debug("tcp client run");
        this.sandboxIo.Run();
    }

    /// <summary>
    /// Wait until this client exits.
    /// </summary>
    public void WaitForExit()
    {
        this.sandboxIo.WaitForExit();
    }

    /// <summary>
    /// Stop this client. Sets the stop flag, cancels any outstanding backoff
    /// wait (so a reconnecting client exits without sleeping out the window),
    /// cancels the live connection's token source, and shuts down the socket.
    /// Idempotent.
    /// </summary>
    public void Stop()
    {
        try
        {
            this.stopFlag = true;
            Logger.Debug("Cancel reconnect backoff (if any).");
            try
            {
                this.backoffCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // backoffCts was already disposed by the IoHandler exiting; safe to ignore.
            }

            Logger.Debug("Cancel connection.");
            this.connection?.TokenSource.Cancel();
            Logger.Debug("Shut down socket.");
            this.Socket?.Shutdown(SocketShutdown.Both);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Stop client failed.");
        }
        finally
        {
            this.Socket?.Close();
        }
    }

    /// <summary>
    /// Observe a message and register the handler of the message.
    /// </summary>
    /// <param name="key">Message token.</param>
    /// <param name="callback">Callback to handle the message.</param>
    public void RegisterMessageHandler(
        IComparable key,
        Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
    {
        this.msgDispatcher.Register(key, callback);
    }

    /// <summary>
    /// Cancel the observing for a message.
    /// </summary>
    /// <param name="key">Message token.</param>
    /// <param name="callback">Callback to handle the message.</param>
    public void UnregisterMessageHandler(
        IComparable key,
        Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
    {
        this.msgDispatcher.Unregister(key, callback);
    }

    /// <summary>
    /// Generate a unique id (unique for this client only) for message.
    /// Monotonic across reconnects to avoid colliding with peer-side buffers
    /// from the previous socket.
    /// </summary>
    /// <returns>Id generated.</returns>
    private uint GenerateMsgId() => this.counterOfId++;

    private void SendQueueMessageHandler()
    {
        while (!this.stopFlag)
        {
            if (!this.sendQueue.IsEmpty)
            {
                var res = this.sendQueue.TryDequeue(out var tp);
                if (res)
                {
                    var (client, msg, reentry) = tp;

                    var id = client.GenerateMsgId();

                    // if (!reentry)
                    // {
                    //     tokenSequence_.Enqueue(id);
                    // }
                    var pkg = PackageHelper.FromProtoBuf(msg, id);

                    // Drop messages during the reconnect window. Callers awaiting
                    // on the RPC will surface as RpcException/timeout at the
                    // await site - intentional per the reconnect plan (D2).
                    if (!client.IsConnected)
                    {
                        Logger.Warn(
                            $"Dropping {msg.Descriptor?.Name} (reentry={reentry}) " +
                            $"to {client.targetIp}:{client.targetPort}: client disconnected (attempt #{client.ReconnectAttempt}).");
                        continue;
                    }

                    try
                    {
                        client.Socket!.Send(pkg.ToBytes().Span);
                    }
                    catch (Exception e)
                    {
                        // Do NOT Stop() here. The receive-side IoHandler will
                        // observe the same socket failure and drive the
                        // reconnect state machine. Just log and drop.
                        Logger.Warn(
                            $"Send {msg.Descriptor?.Name} failed; receive loop will trigger reconnect. {e.Message}");
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Top-level IO state machine. On entry, attempts the initial bounded-retry
    /// connect (preserves the original ConnectRetryMaxTimes=10 boot-fail-fast
    /// behavior). On success, loops between Connected (run message receive
    /// loop) and Reconnecting (wait backoff + single connect attempt) until
    /// stopFlag is set.
    /// </summary>
    private async Task IoHandler()
    {
        this.OnInit?.Invoke(this);

        try
        {
            if (!await this.TryInitialConnectAsync())
            {
                // Initial connect failed permanently (ConnectRetryMaxTimes exhausted).
                // Preserve original behavior: throw out via OnDispose. Caller
                // will be re-spawned by the supervisor at the process level.
                this.OnDispose?.Invoke(this);
                throw new Exception($"Target {this.targetIp} {this.targetPort} cannot be connected.");
            }

            while (!this.stopFlag)
            {
                try
                {
                    await this.RunReceiveLoopAsync(this.connection!);
                }
                catch (Exception e)
                {
                    Logger.Warn($"TcpClient receive loop to {this.targetIp}:{this.targetPort} ended: {e.Message}");
                }
                finally
                {
                    try
                    {
                        await this.connection!.TokenSource.CancelAsync();
                    }
                    catch
                    {
                        // already-disposed/cancelled token source is fine.
                    }
                }

                if (this.stopFlag)
                {
                    break;
                }

                // Connection just died mid-run. Surface to call site so it can
                // mark in-flight RPCs as failed if it tracks them.
                Logger.Info($"TcpClient to {this.targetIp}:{this.targetPort} disconnected; entering reconnect backoff.");
                try
                {
                    this.OnDisconnected?.Invoke(this);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "OnDisconnected callback threw; continuing reconnect.");
                }

                if (!await this.TryReconnectAsync())
                {
                    // TryReconnectAsync only returns false when stopFlag flips
                    // mid-backoff (e.g. Stop() was called). In that case exit
                    // cleanly via finally.
                    break;
                }

                // Successful reconnect - fire OnReconnected so the call site
                // can re-send Control{Restart}, re-register handlers etc.
                try
                {
                    this.OnReconnected?.Invoke(this);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "OnReconnected callback threw; staying connected, may need another reconnect.");
                }
            }
        }
        finally
        {
            this.OnDispose?.Invoke(this);
        }
    }

    private async Task<bool> TryInitialConnectAsync()
    {
        var ipa = IPAddress.Parse(this.targetIp);
        var ipe = new IPEndPoint(ipa, this.targetPort);
        this.Socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        Logger.Debug($"Connect to: {this.targetIp}:{this.targetPort}");

        int retryTimes = 0;
        while (!this.Socket.Connected && !this.stopFlag && retryTimes <= ConnectRetryMaxTimes)
        {
            try
            {
                await this.Socket.ConnectAsync(ipe);
            }
            catch (Exception e)
            {
                ++retryTimes;
                Logger.Error(
                    e,
                    $"Connect failed {this.targetIp}:{this.targetPort}, retry for {retryTimes} times...");
                await Task.Delay(1000);
            }
        }

        if (!this.Socket.Connected)
        {
            this.Socket = null;
            var ex = new Exception($"Target {this.targetIp} {this.targetPort} cannot be connected.");
            Logger.Fatal(ex, $"Target {this.targetIp} {this.targetPort} cannot be connected.");
            return false;
        }

        this.AttachConnection();

        Logger.Info($"Connect to {this.targetIp}:{this.targetPort} succ.");
        this.firstConnectDone = true;
        this.ReconnectAttempt = 0;
        this.OnConnected?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Single reconnect attempt loop: wait backoff, try one connect, on failure
    /// increment attempt and loop. Returns false only if stopFlag flips during
    /// the wait.
    /// </summary>
    private async Task<bool> TryReconnectAsync()
    {
        while (!this.stopFlag)
        {
            var backoffMs = this.ComputeBackoffMs();
            Logger.Info(
                $"TcpClient reconnect to {this.targetIp}:{this.targetPort} attempt " +
                $"#{this.ReconnectAttempt + 1} in {backoffMs} ms.");
            try
            {
                await Task.Delay(backoffMs, this.backoffCts.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (this.stopFlag)
            {
                return false;
            }

            try
            {
                var ipa = IPAddress.Parse(this.targetIp);
                var ipe = new IPEndPoint(ipa, this.targetPort);
                this.Socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await this.Socket.ConnectAsync(ipe);
            }
            catch (Exception e)
            {
                ++this.ReconnectAttempt;
                Logger.Warn($"Reconnect attempt #{this.ReconnectAttempt} failed: {e.Message}");
                this.Socket?.Close();
                this.Socket = null;
                continue;
            }

            this.AttachConnection();
            this.ReconnectAttempt = 0;
            Logger.Info($"Reconnected to {this.targetIp}:{this.targetPort}.");
            return true;
        }

        return false;
    }

    private void AttachConnection()
    {
        var cts = new CancellationTokenSource();
        this.connection = new SocketConnection(this.Socket!, cts);
        this.connection.Connect();
    }

    private int ComputeBackoffMs()
    {
        // 1s, 2s, 4s, 8s, 16s, 30s, 30s, ... with +/- 20% jitter.
        var shift = Math.Min(this.ReconnectAttempt, 5);
        var raw = Math.Min(ReconnectBackoffMaxMs, ReconnectBackoffBaseMs * (1 << shift));
        var jitter = (int)(raw * 0.2 * (JitterRng.NextDouble() - 0.5) * 2);
        return Math.Max(100, raw + jitter);
    }

    private Task RunReceiveLoopAsync(SocketConnection conn) =>
        RpcHelper.HandleMessage(
            conn,
            () => this.stopFlag,
            (msg) => this.bus.AppendMessage(msg),
            null);
}
