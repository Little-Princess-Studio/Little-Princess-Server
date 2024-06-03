// -----------------------------------------------------------------------
// <copyright file="ServiceManagerConnectionManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.ConnectionManager;

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;

/// <summary>
/// Manages connections for the service manager, including gate, service, and server connections.
/// </summary>
internal class ServiceManagerConnectionManager
{
    private readonly ConnectionMap gateConnectionMap;
    private readonly ConnectionMap serviceConnectionMap;
    private readonly ConnectionMap serverConnectionMap;
    private readonly Random random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceManagerConnectionManager"/> class.
    /// </summary>
    public ServiceManagerConnectionManager()
    {
        this.gateConnectionMap = new();
        this.serviceConnectionMap = new();
        this.serverConnectionMap = new();
    }

    /// <summary>
    /// Sends a message to a connection of the specified type with the given ID.
    /// </summary>
    /// <param name="id">The ID of the connection to send the message to.</param>
    /// <param name="msg">The message to send.</param>
    /// <param name="connectionType">The type of connection to send the message to.</param>
    public void SendMessage(string id, IMessage msg, ConnectionType connectionType)
    {
        switch (connectionType)
        {
            case ConnectionType.Gate:
                this.gateConnectionMap.SendMessage(id, msg);
                break;
            case ConnectionType.Service:
                this.serviceConnectionMap.SendMessage(id, msg);
                break;
            case ConnectionType.Server:
                this.serverConnectionMap.SendMessage(id, msg);
                break;
            default:
                Logger.Warn($"Invalid connection type {connectionType}.");
                break;
        }
    }

    /// <summary>
    /// Sends a message to a random gate.
    /// </summary>
    /// <param name="msg">The message to send.</param>
    public void SendMessageToRandomGate(IMessage msg)
    {
        string connId = string.Empty;
        var immediateConnIds = this.gateConnectionMap.GetConnections();
        if (immediateConnIds.Length > 0)
        {
            connId = immediateConnIds[this.random.Next(0, immediateConnIds.Length)];
        }

        if (string.IsNullOrEmpty(connId))
        {
            throw new Exception("No gate connections available.");
        }

        this.gateConnectionMap.SendMessage(connId, msg);
    }

    /// <summary>
    /// Registers a connection immediately based on the connection type and ID.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <param name="connectionType">The type of connection to register.</param>
    /// <param name="id">The ID of the connection to register.</param>
    public void RegisterConnection(Connection connection, ConnectionType connectionType, string id)
    {
        switch (connectionType)
        {
            case ConnectionType.Gate:
                this.gateConnectionMap.RegisterConnection(id, connection);
                break;
            case ConnectionType.Service:
                this.serviceConnectionMap.RegisterConnection(id, connection);
                break;
            case ConnectionType.Server:
                this.serverConnectionMap.RegisterConnection(id, connection);
                break;
            default:
                break;
        }
    }

#pragma warning disable SA1600
#pragma warning disable SA1602
    internal enum ConnectionType
    {
        Server,
        Gate,
        Service,
        Http,
    }

    private class ConnectionMap
    {
        private readonly Dictionary<string, Connection> connectionMap = new();

        public bool HasConnection(string id)
            => this.connectionMap.ContainsKey(id);

        public void RegisterConnection(string id, Connection connection)
            => this.connectionMap[id] = connection;

        public string[] GetConnections() => this.connectionMap.Keys.ToArray();

        public Connection? GetConnectionById(string id)
        {
            this.connectionMap.TryGetValue(id, out var value);
            return value;
        }

        public void SendMessage(string id, IMessage msg)
        {
            if (this.HasConnection(id))
            {
                var conn = this.connectionMap[id];
                conn.Send(msg);
            }
            else
            {
                Logger.Warn($"Connection {id} not found.");
            }
        }
    }
#pragma warning restore SA1602
#pragma warning restore SA1600
}