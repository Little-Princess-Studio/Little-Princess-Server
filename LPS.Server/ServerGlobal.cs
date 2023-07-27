// -----------------------------------------------------------------------
// <copyright file="ServerGlobal.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server;

using System;
using LPS.Server.Instance;

/// <summary>
/// ServerGlobal variable.
/// </summary>
public static class ServerGlobal
{
    private static IInstance instance = null!;

    /// <summary>
    /// Gets instance type.
    /// </summary>
    public static InstanceType InstanceType => instance.InstanceType;

    /// <summary>
    /// Gets the instance as Server.
    /// </summary>
    /// <exception cref="Exception">Throw exception if current process instance is not Server.</exception>
    public static Server Server
    {
        get
        {
            if (instance.InstanceType != InstanceType.Server)
            {
                throw new Exception("Instance is not Server");
            }

            return (instance as Server)!;
        }
    }

    /// <summary>
    /// Gets the instance as Gate.
    /// </summary>
    /// <exception cref="Exception">Throw exception if current process instance is not Gate.</exception>
    public static Gate Gate
    {
        get
        {
            if (instance.InstanceType != InstanceType.Gate)
            {
                throw new Exception("Instance is not Server");
            }

            return (instance as Gate)!;
        }
    }

    /// <summary>
    /// Gets the instance as HostManager.
    /// </summary>
    /// <exception cref="Exception">Throw exception if current process instance is not HostManager.</exception>
    public static HostManager HostManager
    {
        get
        {
            if (instance.InstanceType != InstanceType.HostManager)
            {
                throw new Exception("Instance is not Server");
            }

            return (instance as HostManager)!;
        }
    }

    /// <summary>
    /// Gets the instance as DbManager.
    /// </summary>
    /// <exception cref="Exception">Throw exception if current process instance is not DbManager.</exception>
    public static DbManager DbManager
    {
        get
        {
            if (instance.InstanceType != InstanceType.DbManager)
            {
                throw new Exception("Instance is not Server");
            }

            return (instance as DbManager)!;
        }
    }

    /// <summary>
    /// Init ServerGlobal.
    /// </summary>
    /// <param name="instance">Process instance object.</param>
    /// <exception cref="ArgumentNullException">Argument of <paramref name="instance"/> must not null.</exception>
    /// <exception cref="Exception">ServerGlobal should not be re-initialized.</exception>
    public static void Init(IInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (ServerGlobal.instance != null)
        {
            throw new Exception("instance global can not be re-init");
        }

        ServerGlobal.instance = instance!;
    }
}

/// <summary>
/// Instance interface of the process.
/// </summary>
public interface IInstance
{
    /// <summary>
    /// Gets the type of current process instance.
    /// </summary>
    InstanceType InstanceType { get; }

    /// <summary>
    /// Gets the ip of the instance.
    /// </summary>
    public string Ip { get; }

    /// <summary>
    /// Gets the port of the instance.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets the hostnum of the instance.
    /// </summary>
    public int HostNum { get; }

    /// <summary>
    /// Gets the name of the instance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Main loop of the instance process.
    /// </summary>
    void Loop();

    /// <summary>
    /// Stop the instance process.
    /// </summary>
    void Stop();
}

/// <summary>
/// Instance type enum.
/// </summary>
public enum InstanceType
{
    /// <summary>
    /// Server.
    /// </summary>
    Server,

    /// <summary>
    /// Gete.
    /// </summary>
    Gate,

    /// <summary>
    /// HostManager.
    /// </summary>
    HostManager,

    /// <summary>
    /// Database Manager.
    /// </summary>
    DbManager,

    /// <summary>
    /// Service.
    /// </summary>
    Service,

    /// <summary>
    /// Service router.
    /// </summary>
    ServiceManager,
}