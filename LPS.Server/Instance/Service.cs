// -----------------------------------------------------------------------
// <copyright file="Service.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Service.Instance;

using System;
using LPS.Server;
using LPS.Server.Instance.HostConnection;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Rpc;

/// <summary>
/// Represents a service instance, which contains multiple LPS service instance.
/// </summary>
public class Service : IInstance
{
    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.Service;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    private readonly IManagerConnection serviceMgrConnection;

    private uint acyncId;

    /// <summary>
    /// Initializes a new instance of the <see cref="Service"/> class.
    /// </summary>
    /// <param name="serviceMgrIp">The IP address of the service manager.</param>
    /// <param name="serviceMgrPort">The port number of the service manager.</param>
    /// <param name="name">The name of the service instance.</param>
    /// <param name="ip">The IP address of the service instance.</param>
    /// <param name="port">The port number of the service instance.</param>
    /// <param name="hostNum">The number of the host.</param>
    public Service(string serviceMgrIp, int serviceMgrPort, string name, string ip, int port, int hostNum)
    {
        this.serviceMgrConnection = new ImmediateServiceManagerConnectionOfService(
            serviceMgrIp,
            serviceMgrPort,
            this.GenerateConnectionId,
            checkServerStopped: () => false);

        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
    }

    /// <inheritdoc/>
    public void Loop()
    {
        this.serviceMgrConnection.Run();
        this.serviceMgrConnection.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.serviceMgrConnection.ShutDown();
    }

    private uint GenerateConnectionId()
    {
        return this.acyncId++;
    }
}