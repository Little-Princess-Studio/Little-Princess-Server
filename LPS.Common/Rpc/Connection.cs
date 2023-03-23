// -----------------------------------------------------------------------
// <copyright file="Connection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Net.Sockets;

/// <summary>
/// Connection status of the remote connection.
/// </summary>
public enum ConnectStatus
{
    /// <summary>
    /// Initial status.
    /// </summary>
    Init = 0,

    /// <summary>
    /// Already connected.
    /// </summary>
    Connected,

    /// <summary>
    /// Trying to reconnect.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Already disconnected.
    /// </summary>
    Disconnected,
}

/// <summary>
/// Connection object represents a remote connection.
/// </summary>
public class Connection
{
    /// <summary>
    /// Gets the status of the connection status.
    /// </summary>
    public ConnectStatus Status { get; private set; }

    /// <summary>
    /// Gets the socket of the connection.
    /// </summary>
    public Socket Socket { get; private init; } = null!;

    /// <summary>
    /// Gets the cancellationTokenSource of the connection.
    /// </summary>
    public CancellationTokenSource TokenSource { get; private init; } = null!;

    /// <summary>
    /// Gets or sets the MailBox of remote.
    /// </summary>
    public MailBox MailBox { get; set; }

    /// <summary>
    /// Gets or sets the connection id of this connection.
    /// </summary>
    public uint ConnectionId { get; set; } = uint.MaxValue;

    private Connection()
    {
    }

    /// <summary>
    /// Create a connection.
    /// </summary>
    /// <param name="socket">Socket of the connection.</param>
    /// <param name="tokenSource">CancellationTokenSource of the connection used to disconnect the connection.</param>
    /// <returns>Connection.</returns>
    public static Connection Create(Socket socket, CancellationTokenSource tokenSource)
    {
        var newConnection = new Connection
        {
            Status = ConnectStatus.Init,
            Socket = socket,
            TokenSource = tokenSource,
        };
        return newConnection;
    }

    /// <summary>
    /// Set connection status to Connected.
    /// </summary>
    public void Connect()
    {
        this.Status = ConnectStatus.Connected;
    }

    /// <summary>
    /// Set connection status to Disconnected.
    /// </summary>
    public void Disconnect()
    {
        this.Status = ConnectStatus.Disconnected;
    }
}