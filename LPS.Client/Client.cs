// -----------------------------------------------------------------------
// <copyright file="Client.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using kcp2k;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;

/// <summary>Transport flavours the client SDK can speak.</summary>
public enum ClientTransport
{
    /// <summary>Length-prefixed framing over TCP.</summary>
    Tcp,

    /// <summary>kcp2k-backed reliable UDP. One application message per datagram.</summary>
    Kcp,
}

/// <summary>
/// Client class.
/// </summary>
public class Client
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly Client Instance = new();

#pragma warning disable SA1305
    private readonly SandBox ioSandBox;
#pragma warning restore SA1305
    private readonly SandBox sendSandBox;
    private readonly SandBox pumpSandBox;
    private readonly Bus bus;
    private readonly Dispatcher<(IMessage, Connection, uint)> msgDispatcher;
    private readonly ConcurrentQueue<IMessage> sendQueue = new();

    private string? ip;
    private int port;
    private ClientTransport transport = ClientTransport.Tcp;
    private Socket? socket;
    private KcpClient? kcpClient;
    private KcpConnection? kcpConn;
    private bool exitFlag;
    private uint packageId;

    private Client()
    {
        this.msgDispatcher = new Dispatcher<(IMessage, Connection, uint)>();
        this.bus = new Bus(this.msgDispatcher);

        this.ioSandBox = SandBox.Create(this.IoHandler);
        this.sendSandBox = SandBox.Create(this.SendHandler);
        this.pumpSandBox = SandBox.Create(this.PumpHandler);
    }

    /// <summary>
    /// Register message handler.
    /// </summary>
    /// <param name="key">Message token.</param>
    /// <param name="callback">Handler of the message.</param>
    public void RegisterMessageHandler(
        IComparable key,
        Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
    {
        this.msgDispatcher.Register(key, callback);
    }

    /// <summary>
    /// Unregister message handler.
    /// </summary>
    /// <param name="key">Message token.</param>
    /// <param name="callback">Handler of the message.</param>
    public void UnregisterMessageHandler(
        IComparable key,
        Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
    {
        this.msgDispatcher.Unregister(key, callback);
    }

    /// <summary>
    /// Init client.
    /// </summary>
    /// <param name="ip">Ip.</param>
    /// <param name="port">Port (TCP listener port, OR KCP UDP port depending on transport).</param>
    /// <param name="transport">Transport flavour - TCP or KCP. Defaults to TCP for backwards compatibility.</param>
    public void Init(string ip, int port, ClientTransport transport = ClientTransport.Tcp)
    {
        this.ip = ip;
        this.port = port;
        this.transport = transport;
    }

    /// <summary>
    /// Start the client.
    /// </summary>
    public void Start()
    {
        this.ioSandBox.Run();
    }

    /// <summary>
    /// Wait for all the thread exits.
    /// </summary>
    public void WaitForExit()
    {
        this.sendSandBox.WaitForExit();
        this.ioSandBox.WaitForExit();
        this.pumpSandBox.WaitForExit();
    }

    /// <summary>
    /// Stop client.
    /// </summary>
    public void Stop()
    {
        this.exitFlag = true;
    }

    /// <summary>
    /// Send message to server.
    /// </summary>
    /// <param name="msg">Protobuf message.</param>
    public void Send(IMessage msg)
    {
        try
        {
            this.sendQueue.Enqueue(msg);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Send Error.");
        }
    }

    private void PumpHandler()
    {
        while (!this.exitFlag)
        {
            try
            {
                this.bus.Pump();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Pump message failed.");
            }
            finally
            {
                Thread.Sleep(1);
            }
        }
    }

    private void SendHandler()
    {
        while (!this.exitFlag)
        {
            if (!this.sendQueue.IsEmpty)
            {
                var res = this.sendQueue.TryDequeue(out var msg);
                if (res)
                {
                    var id = RpcClientHelper.GenerateRpcId();
                    var pkg = PackageHelper.FromProtoBuf(msg!, id);
                    try
                    {
                        if (this.transport == ClientTransport.Tcp)
                        {
                            this.socket!.Send(pkg.ToBytes().Span);
                        }
                        else
                        {
                            // KCP path: hand the same Package bytes to
                            // kcp2k as a single datagram. kcp2k delivers
                            // it on the server's OnData callback intact.
                            this.kcpConn!.Send(pkg.ToBytes());
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Send msg failed.");
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    private async Task IoHandler()
    {
        if (this.transport == ClientTransport.Kcp)
        {
            await this.KcpIoHandler();
            return;
        }

        var ipa = IPAddress.Parse(this.ip!);
        var ipe = new IPEndPoint(ipa, this.port);

        this.socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        Logger.Debug($"Connect to gate via TCP: {this.ip}:{this.port}");
        await this.socket.ConnectAsync(ipe);

        if (!this.socket.Connected)
        {
            this.socket = null;
            var e = new Exception($"Target cannot be connected.");
            Logger.Fatal(e, $"Target cannot be connected.");
            throw e;
        }

        Logger.Debug("Connected to Gate.");

        this.sendSandBox.Run();
        this.pumpSandBox.Run();

        var cancellationTokenSource = new CancellationTokenSource();
        var conn = new SocketConnection(this.socket, cancellationTokenSource);
        conn.Connect();

        while (!this.exitFlag)
        {
            await this.HandleMessage(conn);
        }

        cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// KCP-flavour I/O loop. kcp2k is single-threaded cooperative - we
    /// drive its <see cref="KcpClient.Tick"/> on this SandBox at the
    /// configured interval and route <c>OnData</c> callbacks straight into
    /// the same <see cref="Bus"/> the TCP path uses, so handler
    /// registration is transport-independent.
    /// </summary>
    private Task KcpIoHandler()
    {
        // Mirror server-side defaults so handshake constants match.
        var config = new KcpConfig(
            DualMode: false,
            NoDelay: true,
            Interval: 10,
            Timeout: 10000,
            FastResend: 2,
            CongestionWindow: false);

        var connectedSignal = new ManualResetEventSlim(false);

        this.kcpClient = new KcpClient(
            OnConnected: () =>
            {
                Logger.Debug("[Client/KCP] Connected.");
                connectedSignal.Set();
            },
            OnData: (segment, channel) =>
            {
                try
                {
                    var pkg = PackageHelper.GetPackage(segment.AsSpan());
                    var type = (PackageType)pkg.Header.Type;
                    var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
                    var arg = (pb, (Connection)this.kcpConn!, pkg.Header.ID);
                    this.bus.AppendMessage(new Message(type, arg));
                }
                catch (Exception e)
                {
                    Logger.Error(e, "[Client/KCP] parse failed.");
                }
            },
            OnDisconnected: () =>
            {
                Logger.Info("[Client/KCP] Disconnected.");
                this.exitFlag = true;
            },
            OnError: (code, reason) => Logger.Warn($"[Client/KCP] error {code}: {reason}"),
            config: config);

        this.kcpConn = KcpConnection.ForClient(this.kcpClient);
        this.kcpConn.Connect();

        Logger.Debug($"Connect to gate via KCP: {this.ip}:{this.port}");
        this.kcpClient.Connect(this.ip!, (ushort)this.port);

        // Start send + pump only after KCP handshake completes so early
        // sends don't get dropped before connection ready.
        var initTask = Task.Run(() =>
        {
            // Drive tick until handshake done.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!connectedSignal.IsSet && DateTime.UtcNow < deadline && !this.exitFlag)
            {
                this.kcpClient.Tick();
                Thread.Sleep((int)config.Interval);
            }

            if (!connectedSignal.IsSet)
            {
                Logger.Fatal(new Exception("KCP handshake timeout"), "[Client/KCP] handshake timed out.");
                this.exitFlag = true;
                return;
            }

            this.sendSandBox.Run();
            this.pumpSandBox.Run();

            while (!this.exitFlag)
            {
                this.kcpClient.Tick();
                Thread.Sleep((int)config.Interval);
            }

            this.kcpClient.Disconnect();
        });

        return initTask;
    }

    private Task HandleMessage(SocketConnection conn) =>
        RpcHelper.HandleMessage(
            conn,
            () => this.exitFlag,
            msg => this.bus.AppendMessage(msg),
            () => this.exitFlag = true);
}