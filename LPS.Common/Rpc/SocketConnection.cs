// -----------------------------------------------------------------------
// <copyright file="SocketConnection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Net.Sockets;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// Socket connection object represents a remote connection using socket to connect to remote.
/// </summary>
public sealed class SocketConnection : Connection
{
    /// <summary>
    /// Gets the socket of the connection.
    /// </summary>
    public Socket Socket { get; private init; } = null!;

    /// <summary>
    /// Gets the cancellationTokenSource of the connection.
    /// </summary>
    public CancellationTokenSource TokenSource { get; protected init; } = null!;

    private SocketConnection()
    {
    }

    /// <summary>
    /// Create a connection.
    /// </summary>
    /// <param name="socket">Socket of the connection.</param>
    /// <param name="tokenSource">CancellationTokenSource of the connection used to disconnect the connection.</param>
    /// <returns>Connection.</returns>
    public static SocketConnection Create(Socket socket, CancellationTokenSource tokenSource)
    {
        var newConnection = new SocketConnection
        {
            Status = ConnectStatus.Init,
            Socket = socket,
            TokenSource = tokenSource,
        };
        return newConnection;
    }

    /// <summary>
    /// Set connection status to Disconnected.
    /// </summary>
    public override void Disconnect()
    {
        try
        {
            Logger.Debug($"connect disconnect invoked.");
            Logger.Debug($"Stack Trace: {System.Environment.StackTrace}");
            this.Status = ConnectStatus.Disconnected;
            this.OnDisconnected?.Invoke();
        }
        catch (Exception e)
        {
            Logger.Error(e, "OnDisconnected handler exception.");
        }
    }

    /// <inheritdoc/>
    public override void Send(IMessage message)
    {
        var pkg = PackageHelper.FromProtoBuf(message, 0);
        this.Socket.Send(pkg.ToBytes());
    }

    /// <inheritdoc/>
    public override void Send(byte[] bytes)
    {
        this.Socket.Send(bytes);
    }
}