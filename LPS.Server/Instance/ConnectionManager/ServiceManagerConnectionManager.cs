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
    private readonly Random random = new Random();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceManagerConnectionManager"/> class.
    /// </summary>
    /// <param name="gateConnectionMap">The connection map for gate connections.</param>
    /// <param name="serviceConnectionMap">The connection map for service connections.</param>
    /// <param name="serverConnectionMap">The connection map for server connections.</param>
    public ServiceManagerConnectionManager(
        ConnectionMap gateConnectionMap,
        ConnectionMap serviceConnectionMap,
        ConnectionMap serverConnectionMap)
    {
        this.gateConnectionMap = gateConnectionMap;
        this.serviceConnectionMap = serviceConnectionMap;
        this.serverConnectionMap = serverConnectionMap;
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
        var sendImmediately = this.random.Next(0, 2) == 0;
        string connId = string.Empty;
        if (sendImmediately)
        {
            var immediateConnIds = this.gateConnectionMap.GetImmediateConnectionConnections();
            if (immediateConnIds.Length > 0)
            {
                connId = immediateConnIds[this.random.Next(0, immediateConnIds.Length)];
            }
            else
            {
                var msgConnIds = this.gateConnectionMap.GetAllMessageQueueIdentifiers();
                if (msgConnIds.Length > 0)
                {
                    connId = msgConnIds[this.random.Next(0, msgConnIds.Length)];
                }
            }
        }
        else
        {
            var msgConnIds = this.gateConnectionMap.GetAllMessageQueueIdentifiers();
            if (msgConnIds.Length > 0)
            {
                connId = msgConnIds[this.random.Next(0, msgConnIds.Length)];
            }
            else
            {
                var immediateConnIds = this.gateConnectionMap.GetImmediateConnectionConnections();
                if (immediateConnIds.Length > 0)
                {
                    connId = immediateConnIds[this.random.Next(0, immediateConnIds.Length)];
                }
            }
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
    public void RegisterImmediateConnection(Connection connection, ConnectionType connectionType, string id)
    {
        switch (connectionType)
        {
            case ConnectionType.Gate:
                this.gateConnectionMap.RegisterImmediateConnection(id, connection);
                break;
            case ConnectionType.Service:
                this.serviceConnectionMap.RegisterImmediateConnection(id, connection);
                break;
            case ConnectionType.Server:
                this.serverConnectionMap.RegisterImmediateConnection(id, connection);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Registers a message queue connection for the specified connection type, id, and identifier.
    /// </summary>
    /// <param name="connectionType">The type of connection to register.</param>
    /// <param name="id">The ID of the connection to register.</param>
    /// <param name="identifier">The identifier of the connection to register.</param>
    public void RegisterMessageQueueConnection(ConnectionType connectionType, string id, string identifier)
    {
        switch (connectionType)
        {
            case ConnectionType.Gate:
                this.gateConnectionMap.RegisterMessageQueueConnection(id, identifier);
                break;
            case ConnectionType.Service:
                this.serviceConnectionMap.RegisterMessageQueueConnection(id, identifier);
                break;
            case ConnectionType.Server:
                this.serverConnectionMap.RegisterMessageQueueConnection(id, identifier);
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

    internal class ConnectionMap
    {
        private readonly Dictionary<string, Connection> immediateConnectionMap = new();
        private readonly Dictionary<string, string> msgqQueueConnectionMap = new();
        private readonly Action<Connection, IMessage> sendImmediateMessage;
        private readonly Action<string, IMessage> sendMessageQueueMessage;

        public ConnectionMap(
            Action<Connection, IMessage> sendImmediateMessage,
            Action<string, IMessage> sendMessageQueueMessage)
        {
            this.sendImmediateMessage = sendImmediateMessage;
            this.sendMessageQueueMessage = sendMessageQueueMessage;
        }

        public bool IsImmediateConnection(string id)
            => this.immediateConnectionMap.ContainsKey(id);

        public bool IsMessageQueueConnection(string id)
            => this.msgqQueueConnectionMap.ContainsKey(id);

        public void RegisterImmediateConnection(string id, Connection connection)
            => this.immediateConnectionMap[id] = connection;

        public void RegisterMessageQueueConnection(string id, string identifier)
            => this.msgqQueueConnectionMap[id] = identifier;

        public string[] GetImmediateConnectionConnections() => this.immediateConnectionMap.Keys.ToArray();

        public string[] GetAllMessageQueueIdentifiers() => this.msgqQueueConnectionMap.Keys.ToArray();

        public Connection? GetConnectionById(string id)
        {
            if (this.IsImmediateConnection(id))
            {
                return this.immediateConnectionMap[id];
            }

            return null;
        }

        public string? GetIdentifierById(string id)
        {
            if (this.IsMessageQueueConnection(id))
            {
                return this.msgqQueueConnectionMap[id];
            }

            return null;
        }

        public void SendMessage(string id, IMessage msg)
        {
            if (this.IsImmediateConnection(id))
            {
                var conn = this.immediateConnectionMap[id];
                this.sendImmediateMessage(conn, msg);
            }
            else if (this.IsMessageQueueConnection(id))
            {
                var identifier = this.msgqQueueConnectionMap[id];
                this.sendMessageQueueMessage(identifier, msg);
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