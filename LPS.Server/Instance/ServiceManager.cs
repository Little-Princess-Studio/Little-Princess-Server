// -----------------------------------------------------------------------
// <copyright file="ServiceManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Service.Instance;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server;
using LPS.Server.Database;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using LPS.Server.Service;

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
    private readonly Dictionary<Common.Rpc.MailBox, Connection> mailBoxToServiceConn = new();
    private readonly int desiredServiceNum;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceManager"/> class.
    /// </summary>
    /// <param name="name">Name of the server.</param>
    /// <param name="ip">Ip of the server.</param>
    /// <param name="port">Port of the server.</param>
    /// <param name="hostNum">Hostnum of the server.</param>
    /// <param name="hostManagerIp">Ip of the hostmanager.</param>
    /// <param name="hostManagerPort">Port of the hostmanager.</param>
    /// <param name="useMqToHostMgr">If use message queue to build connection with host manager.</param>
    /// <param name="desiredServiceNum">The desired number of services to be registered.</param>
    public ServiceManager(
        string name,
        string ip,
        int port,
        int hostNum,
        int desiredServiceNum)
    {
        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
        this.desiredServiceNum = desiredServiceNum;
        this.tcpServer = new TcpServer(this.Ip, this.Port)
        {
            OnInit = this.RegisterServerMessageHandlers,
            OnDispose = this.UnregisterServerMessageHandlers,
        };
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

    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.ServiceRpc, this.HandleServiceRpc);
        this.tcpServer.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleServiceRpcCallBack);
        this.tcpServer.RegisterMessageHandler(PackageType.ServiceControl, this.HandleServiceControl);
    }

    private void UnregisterServerMessageHandlers()
    {
        this.tcpServer.UnregisterMessageHandler(PackageType.ServiceRpc, this.HandleServiceRpc);
        this.tcpServer.UnregisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleServiceRpcCallBack);
        this.tcpServer.UnregisterMessageHandler(PackageType.ServiceControl, this.HandleServiceControl);
    }

    private void HandleServiceRpc((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        // var serviceRpc = arg.Message as ServiceRpc;
    }

    private void HandleServiceRpcCallBack((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        // var serviceRpc = arg.Message as ServiceRpcCallBack;
    }

    private void HandleServiceControl((IMessage Message, Connection Connection, uint RpcId) tuple)
    {
        var (msg, conn, _) = tuple;
        var ctl = (ServiceControl)msg;

        switch (ctl.Message)
        {
            case ServiceControlMessage.Ready:
                _ = this.GenerateMailBoxForService(ctl, conn)
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception, $"Failed to generate mailbox for service.");
                        return;
                    }

                    if (this.mailBoxToServiceConn.Count == this.desiredServiceNum)
                    {
                        this.NotifyServiceInstancesToStartServices();
                    }
                });
                break;
            case ServiceControlMessage.Restarted:
                break;
            case ServiceControlMessage.ShutDown:
                break;
        }
    }

    private void NotifyServiceInstancesToStartServices()
    {
        Logger.Info("Notify service instance to create services");

        var assignResult = ServiceHelper.AssignServicesToServiceInstances(this.mailBoxToServiceConn.Count);

        var idx = 0;
        foreach (var conn in this.mailBoxToServiceConn.Values)
        {
            var dict = assignResult[idx++];
            var cmd = new ServiceManagerCommand()
            {
                Type = ServiceManagerCommandType.CreateNewServices,
            };

            var arg = new DictWithStringKeyArg();
            foreach (var pair in dict)
            {
                var serviceName = pair.Key;
                var serviceShards = pair.Value;

                var shardList = new ListArg();
                foreach (var shard in serviceShards)
                {
                    shardList.PayLoad.Add(Any.Pack(new IntArg()
                    {
                        PayLoad = shard,
                    }));
                }

                arg.PayLoad.Add(serviceName, Any.Pack(shardList));
            }

            var pkg = PackageHelper.FromProtoBuf(cmd, 0);
            var bytes = pkg.ToBytes();
            conn.Socket.Send(bytes);
            Logger.Init($"Send command {cmd.Type} to service instance");
        }
    }

    private Task GenerateMailBoxForService(ServiceControl ctlMsg, Connection conn)
    {
        return DbHelper.GenerateNewGlobalId().ContinueWith(t =>
        {
            var id = t.Result;
            var ip = RpcHelper.GetString(ctlMsg.Args[0]);
            var port = RpcHelper.GetInt(ctlMsg.Args[1]);
            var hostNum = RpcHelper.GetInt(ctlMsg.Args[2]);

            var mailBox = new Common.Rpc.MailBox(id, ip, port, hostNum);
            Logger.Info($"Generate mailbox for service {mailBox}.");

            var cmd = new ServiceManagerCommand()
            {
                Type = ServiceManagerCommandType.Start,
            };

            cmd.Args.Add(Any.Pack(new MailBoxArg()
            {
                PayLoad = RpcHelper.RpcMailBoxToPbMailBox(mailBox),
            }));

            var pkg = PackageHelper.FromProtoBuf(cmd, 0);
            var bytes = pkg.ToBytes();

            conn.Socket.Send(bytes);

            lock (this.mailBoxToServiceConn)
            {
                this.mailBoxToServiceConn.Add(mailBox, conn);
            }
        });
    }
}