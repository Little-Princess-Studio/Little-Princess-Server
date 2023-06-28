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
/// </summary>
internal class TcpClient // : IClient
{
    /// <summary>
    /// Gets the socket of the client.
    /// </summary>
    public Socket? Socket { get; private set; }

    /// <summary>
    /// Gets the callback when init client.
    /// </summary>
    public Action<TcpClient>? OnInit { private get; init; }

    /// <summary>
    /// Gets the callback when dispose the client.
    /// </summary>
    public Action<TcpClient>? OnDispose { private get; init; }

    /// <summary>
    /// Gets the callback when connected to server.
    /// </summary>
    public Action<TcpClient>? OnConnected { private get; init; }

    /// <summary>
    /// Gets or sets the mailbox of the client.
    /// </summary>
    public MailBox MailBox { get; set; }

    /// <summary>
    /// Gets the remote port.
    /// </summary>
    public int TargetPort => this.targetPort;

    /// <summary>
    /// Gets the remote IP.
    /// </summary>
    public string TargetIp => this.targetIp;

    private const int ConnectRetryMaxTimes = 10;

    private readonly SandBox sandboxIo;
    private readonly Bus bus;
    private readonly Dispatcher<(IMessage Message, Connection Connection, uint RpcId)> msgDispatcher;
    private readonly string targetIp;
    private readonly int targetPort;
    private readonly TokenSequence<uint> tokenSequence = new();
    private readonly ConcurrentQueue<(TcpClient, IMessage, bool)> sendQueue;
    private readonly SandBox clientsSendQueueSandBox;

    private bool stopFlag;
    private uint counterOfId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClient"/> class.
    /// </summary>
    /// <param name="targetIp">Remote IP.</param>
    /// <param name="targetPort">Remote port.</param>
    /// <param name="sendQueue">Queue to receive and send message to remote server.</param>
    public TcpClient(string targetIp, int targetPort, ConcurrentQueue<(TcpClient TcpClient, IMessage Message, bool IsReentry)> sendQueue)
    {
        this.targetIp = targetIp;
        this.targetPort = targetPort;
        this.sendQueue = sendQueue;

        this.msgDispatcher = new Dispatcher<(IMessage Message, Connection Connection, uint RpcId)>();
        this.bus = new Bus(this.msgDispatcher);

        this.sandboxIo = SandBox.Create(this.IoHandler);
        this.clientsSendQueueSandBox = SandBox.Create(this.SendQueueMessageHandler);
    }

    /// <summary>
    /// Send message to server.
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
    /// Stop this client.
    /// </summary>
    public void Stop()
    {
        this.stopFlag = true;
        try
        {
            this.Socket!.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            this.Socket!.Close();
        }
    }

    /// <summary>
    /// Observe a message and register the handler of the message.
    /// </summary>
    /// <param name="key">Message token.</param>
    /// <param name="callback">Callback to handle the message.</param>
    public void RegisterMessageHandler(IComparable key, Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
    {
        this.msgDispatcher.Register(key, callback);
    }

    /// <summary>
    /// Cancel the observing for a message.
    /// </summary>
    /// <param name="key">Message token.</param>
    /// <param name="callback">Callback to handle the message.</param>
    public void UnregisterMessageHandler(IComparable key, Action<(IMessage Message, Connection Connection, uint RpcId)> callback)
    {
        this.msgDispatcher.Unregister(key, callback);
    }

    /// <summary>
    /// Generate a unique id (unique for this client only) for message.
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

                    try
                    {
                        client.Socket!.Send(pkg.ToBytes());
                    }
                    catch (Exception e)
                    {
                        // TODO: try reconnect
                        Logger.Error(e, $"Send msg {msg} failed.");
                        this.Stop();
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    private async Task IoHandler()
    {
        try
        {
            this.OnInit?.Invoke(this);

            var ipa = IPAddress.Parse(this.targetIp);
            var ipe = new IPEndPoint(ipa, this.targetPort);
            this.Socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Logger.Debug($"Connect to: {this.targetIp}:{this.targetPort}");

            // repeat trying to connect
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
                    Logger.Error(e, $"Connect failed, retry for {retryTimes} times...");
                    Thread.Sleep(1000);
                }
            }

            if (!this.Socket.Connected)
            {
                this.Socket = null;
                var e = new Exception($"Target {this.targetIp} {this.targetPort} cannot be connected.");
                Logger.Fatal(e, $"Target {this.targetIp} {this.targetPort} cannot be connected.");
                throw e;
            }
        }
        catch (Exception)
        {
            this.OnDispose?.Invoke(this);
            throw;
        }

        Logger.Info("Connect to server succ.");
        this.OnConnected?.Invoke(this);

        var cancellationTokenSource = new CancellationTokenSource();
        var conn = Connection.Create(this.Socket, cancellationTokenSource);
        conn.Connect();

        while (!this.stopFlag)
        {
            await this.HandleMessage(conn);
            Thread.Sleep(1);
        }

        cancellationTokenSource.Cancel();
        this.OnDispose?.Invoke(this);
    }

    private async Task HandleMessage(Connection conn) =>
        await RpcHelper.HandleMessage(
            conn,
            () => this.stopFlag,
            (msg) => { this.bus.AppendMessage(msg); },
            null);
}