using System;

namespace LPS.Server.Core;

public interface IInstance
{
    InstanceType InstanceType { get; }
}

public enum InstanceType
{
    Server,
    Gate,
    HostManager,
    DbManager,
}

public static class ServerGlobal
{
    private static IInstance Instance_ = null!;
    public static Server Server
    {
        get
        {
            if (Instance_.InstanceType != InstanceType.Server)
            {
                throw new Exception("Instance is not Server");
            }
            
            return (Instance_ as Server)!;
        }
    }

    public static Gate Gate
    {
        get
        {
            if (Instance_.InstanceType != InstanceType.Gate)
            {
                throw new Exception("Instance is not Server");
            }
            
            return (Instance_ as Gate)!;
        }
    }

    public static HostManager HostManager
    {
        get
        {
            if (Instance_.InstanceType != InstanceType.HostManager)
            {
                throw new Exception("Instance is not Server");
            }
            
            return (Instance_ as HostManager)!;
        }
    }

    public static DbManager DbManager
    {
        get
        {
            if (Instance_.InstanceType != InstanceType.DbManager)
            {
                throw new Exception("Instance is not Server");
            }
            
            return (Instance_ as DbManager)!;
        }
    }

    public static void Init(IInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }
        
        if (Instance_ != null)
        {
            throw new Exception("instance global can not be re-init");
        }
        Instance_ = instance!;
    }
}