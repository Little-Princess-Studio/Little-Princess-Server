// -----------------------------------------------------------------------
// <copyright file="IManagerConnection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using Google.Protobuf;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// HostConnection interface, which is used to connect to the manager instance (Host Manager & Service Manager).
/// </summary>
public interface IManagerConnection
{
    /// <summary>
    /// Start the connection.
    /// </summary>
    void Run();

    /// <summary>
    /// Shut down the connection.
    /// </summary>
    void ShutDown();

    /// <summary>
    /// Wait until the connection to exit.
    /// </summary>
    void WaitForExit();

    /// <summary>
    /// Send protobuf message to hostmanager.
    /// </summary>
    /// <param name="message">Protobuf message.</param>
    void Send(IMessage message);

    /// <summary>
    /// Register the handler to handle message.
    /// </summary>
    /// <param name="packageType">Package type.</param>
    /// <param name="handler">Handler.</param>
    void RegisterMessageHandler(PackageType packageType, Action<IMessage> handler);

    /// <summary>
    /// Unregister the message handler.
    /// </summary>
    /// <param name="packageType">Package type.</param>
    /// <param name="handler">Handler.</param>
    void UnregisterMessageHandler(PackageType packageType, Action<IMessage> handler);
}