// -----------------------------------------------------------------------
// <copyright file="ServiceManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Service.Instance;

using System;
using LPS.Server;
using LPS.Server.Rpc;

/// <summary>
/// Represents a service instance, which contains multiple LPS service instance.
/// </summary>
public class ServiceManager : IInstance
{
    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.ServiceManager;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    private readonly TcpServer tcpServer;
    private readonly Random random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceManager"/> class.
    /// </summary>
    /// <param name="name">The name of the service.</param>
    /// <param name="hostNum">The host number of the service.</param>
    /// <param name="ip">The IP address of the service.</param>
    /// <param name="port">The port number of the service.</param>
    public ServiceManager(string name, int hostNum, string ip, int port)
    {
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
        this.Name = name;

        this.tcpServer = new TcpServer(ip, port);
    }

    /// <inheritdoc/>
    public void Loop()
    {
        this.tcpServer.Run();
        this.tcpServer.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.tcpServer.Stop();
    }
}