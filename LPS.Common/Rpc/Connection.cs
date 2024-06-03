// -----------------------------------------------------------------------
// <copyright file="Connection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Net.Sockets;
using Google.Protobuf;
using LPS.Common.Debug;

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
    /// Already disconnected.
    /// </summary>
    Disconnected,
}

/// <summary>
/// Connection object represents a remote connection.
/// </summary>
public abstract class Connection
{
    /// <summary>
    /// Gets or sets the status of the connection status.
    /// </summary>
    public ConnectStatus Status { get; protected set; }

    /// <summary>
    /// Gets or sets the connection id of this connection, internal usage.
    /// </summary>
    public uint ConnectionId { get; set; } = uint.MaxValue;

    /// <summary>
    /// Gets or sets the MailBox of remote.
    /// </summary>
    public MailBox MailBox { get; set; }

    /// <summary>
    /// Gets or sets the handler when the connection disconnected.
    /// </summary>
    public Action? OnDisconnected { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class.
    /// </summary>
    protected Connection()
    {
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
    public abstract void Disconnect();

    /// <summary>
    /// Send bytes to the remote.
    /// </summary>
    /// <param name="message">Message to send.</param>
    public abstract void Send(IMessage message);

    /// <summary>
    /// Send bytes to the remote.
    /// </summary>
    /// <param name="message">Message to send.</param>
    public abstract void Send(byte[] message);
}