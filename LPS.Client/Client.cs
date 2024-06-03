// -----------------------------------------------------------------------
// <copyright file="Client.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;

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
    private Socket? socket;
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
    /// <param name="port">Port.</param>
    public void Init(string ip, int port)
    {
        this.ip = ip;
        this.port = port;
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
                    var id = this.packageId++;
                    var pkg = PackageHelper.FromProtoBuf(msg!, id);
                    var socket = this.socket!;
                    try
                    {
                        socket.Send(pkg.ToBytes());
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
        var ipa = IPAddress.Parse(this.ip!);
        var ipe = new IPEndPoint(ipa, this.port);

        // todo: auto select net protocol later
        this.socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        Logger.Debug($"Connect to gate: {this.ip}:{this.port}");
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
        var conn = SocketConnection.Create(this.socket, cancellationTokenSource);
        conn.Connect();

        while (!this.exitFlag)
        {
            await this.HandleMessage(conn);
        }

        cancellationTokenSource.Cancel();
    }

    private Task HandleMessage(SocketConnection conn) =>
        RpcHelper.HandleMessage(
            conn,
            () => this.exitFlag,
            msg => this.bus.AppendMessage(msg),
            () => this.exitFlag = true);
}