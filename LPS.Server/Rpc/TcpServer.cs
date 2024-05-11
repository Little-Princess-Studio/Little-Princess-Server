// -----------------------------------------------------------------------
// <copyright file="TcpServer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// TcpServer is common server for inner usage.
/// </summary>
internal class TcpServer
{
    /// <summary>
    /// Gets the Ip of the server.
    /// </summary>
    public string Ip { get; }

    /// <summary>
    /// Gets the port of the server.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets the socket.
    /// </summary>
    public Socket? Socket { get; private set; }

    /// <summary>
    /// Gets the callback when init server.
    /// </summary>
    public Action? OnInit { private get; init; }

    /// <summary>
    /// Gets the callback when dispose the server.
    /// </summary>
    public Action? OnDispose { private get; init; }

    /// <summary>
    /// Gets all the connections on this server.
    /// </summary>
    public Connection[] AllConnections => this.socketToConn.Values.ToArray();

    /// <summary>
    /// Gets the handler invoked every tick of the server.
    /// </summary>
    public Action<uint>? ServerTickHandler { private get; init; }

    /// <summary>
    /// Gets a value indicating whether this server is stopped.
    /// </summary>
    public bool Stopped => this.stopFlag;

    private readonly Dictionary<Connection, Task> connections = new();
    private readonly SandBox sandboxIo;
    private readonly Bus bus;
    private readonly Dispatcher<(IMessage, Connection, uint)> msgDispatcher;
    private readonly Dictionary<Socket, Connection> socketToConn = new();
    private readonly ConcurrentQueue<(Connection, IMessage)> sendQueue = new();
    private bool stopFlag;
    private uint serverEntityPackageId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpServer"/> class.
    /// </summary>
    /// <param name="ip">Ip of the server.</param>
    /// <param name="port">Port of the server.</param>
    public TcpServer(string ip, int port)
    {
        this.Ip = ip;
        this.Port = port;

        this.msgDispatcher = new Dispatcher<(IMessage, Connection, uint)>();
        this.bus = new Bus(this.msgDispatcher);

        this.sandboxIo = SandBox.Create(this.IoHandler);
    }

    /// <summary>
    /// Start server.
    /// </summary>
    public void Run()
    {
        this.stopFlag = false;
        this.sandboxIo.Run();
    }

    /// <summary>
    /// Wait until this server exits.
    /// </summary>
    public void WaitForExit() => this.sandboxIo.WaitForExit();

    /// <summary>
    /// Stop the server.
    /// </summary>
    public void Stop()
    {
        Logger.Debug("stopped");
        this.stopFlag = true;
        try
        {
            this.Socket?.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            this.Socket?.Close();
        }
    }

    /// <summary>
    /// Send message to a client.
    /// </summary>
    /// <param name="msg">Message send to client.</param>
    /// <param name="conn">Connection to a client.</param>
    public void Send(IMessage msg, Connection conn)
    {
        try
        {
            this.sendQueue.Enqueue((conn, msg));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Send Error.");
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

    private void IoHandler()
    {
        #region listen port

        Logger.Debug($"[SOCKET] Start tcp server {this.Ip} {this.Port}");

        this.OnInit?.Invoke();

        var ipa = IPAddress.Parse(this.Ip);
        var ipe = new IPEndPoint(ipa, this.Port);

        this.Socket = new Socket(ipa.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        this.Socket.Bind(ipe);
        this.Socket.Listen(int.MaxValue);

        Logger.Debug("[SOCKET] Listen succ");

        #endregion

        #region init bus pump task

        var busPumpSandBox = SandBox.Create(this.PumpMessageHandler);
        busPumpSandBox.Run();

        #endregion

        #region init send queue task

        var sendQueueSandBox = SandBox.Create(this.SendQueueMessageHandler);
        sendQueueSandBox.Run();

        #endregion

        #region init timecircle task

        var timeCircleSandBox = SandBox.Create(this.TimeCircleHandler);
        timeCircleSandBox.Run();

        #endregion

        #region message loop

        while (!this.stopFlag)
        {
            Socket clientSocket;
            try
            {
                clientSocket = this.Socket!.Accept();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Socket closed.");
                break;
            }

#pragma warning disable SA1305
            var ipEndPoint = (clientSocket.RemoteEndPoint as IPEndPoint)!;
#pragma warning restore SA1305
            Logger.Debug($"New socket got {ipEndPoint.Address}:{ipEndPoint.Port}");

            var cancelTokenSource = new CancellationTokenSource();
            var conn = Connection.Create(clientSocket, cancelTokenSource);
            conn.OnDisconnected = () =>
            {
                Logger.Debug("Client disconnected");
                cancelTokenSource.Cancel();
                this.socketToConn.Remove(clientSocket);
                this.connections.Remove(conn);
            };

            this.socketToConn[clientSocket] = conn;

            conn.Connect();

            var task = this.HandleMessage(conn);
            this.connections[conn] = task;

            task.ContinueWith(
                (t) =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Warn(t.Exception);
                    }

                    Logger.Debug("Client Io Handler Exist");

                    try
                    {
                        conn.Disconnect();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Error when close socket.");
                    }
                },
                cancelTokenSource.Token);
        }

        #endregion

        // cancel each task end
        Logger.Info($"[SOCKET] Close {this.connections.Count} connections");
        foreach (var conn in this.connections)
        {
            if (conn.Key.Status != ConnectStatus.Connected)
            {
                continue;
            }

            conn.Key.TokenSource.Cancel();
            conn.Value.Wait();
        }

        // wait pum task to exit
        Logger.Info("[EXIT] Close pump task");

        // busPumpTask.Wait();
        this.OnDispose?.Invoke();
    }

    private void PumpMessageHandler()
    {
        while (!this.stopFlag)
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
                Thread.Sleep(50);
            }
        }
    }

    private void TimeCircleHandler()
    {
        var lastTimeCircleTickTimestamp = DateTime.UtcNow;

        while (!this.stopFlag)
        {
            var currentTimeCircleTickTimestamp = DateTime.UtcNow;
            var deltaTime = (currentTimeCircleTickTimestamp - lastTimeCircleTickTimestamp).Milliseconds;
            if (deltaTime > 50)
            {
                lastTimeCircleTickTimestamp = currentTimeCircleTickTimestamp;
                this.ServerTickHandler?.Invoke((uint)deltaTime);
            }

            Thread.Sleep(25);
        }
    }

    private void SendQueueMessageHandler()
    {
        while (!this.stopFlag)
        {
            if (!this.sendQueue.IsEmpty)
            {
                var res = this.sendQueue.TryDequeue(out var tp);
                if (res)
                {
                    var (conn, msg) = tp;
                    if (conn.Status != ConnectStatus.Connected)
                    {
                        continue;
                    }

                    var id = this.serverEntityPackageId++;
                    var pkg = PackageHelper.FromProtoBuf(msg, id);
                    var socket = conn.Socket;
                    try
                    {
                        socket.Send(pkg.ToBytes());
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Send msg failed.");
                        conn.TokenSource.Cancel();
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    private Task HandleMessage(Connection conn) =>
        RpcHelper.HandleMessage(
            conn,
            () => this.stopFlag,
            (msg) => this.bus.AppendMessage(msg),
            () => this.connections.Remove(conn));
}