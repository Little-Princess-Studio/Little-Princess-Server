// -----------------------------------------------------------------------
// <copyright file="Service.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Service.Instance;

using System;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server;
using LPS.Server.Instance.HostConnection;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Rpc.InnerMessages;
using LPS.Server.Service;
using Newtonsoft.Json.Linq;

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

    /// <inheritdoc/>
    public JToken Config { get; }

    private readonly IManagerConnection serviceMgrConnection;

    private readonly CountdownEvent waitForMailBox = new(1);

    private readonly Dictionary<string, Dictionary<uint, BaseService>> serviceMap = new();

    private readonly Dictionary<Common.Rpc.MailBox, BaseService> serviceMbMap = new();

    private Common.Rpc.MailBox mailBox;

    private uint acyncId;

    private bool stopFlag;

    /// <summary>
    /// Initializes a new instance of the <see cref="Service"/> class.
    /// </summary>
    /// <param name="serviceMgrIp">The IP address of the service manager.</param>
    /// <param name="serviceMgrPort">The port number of the service manager.</param>
    /// <param name="name">The name of the service instance.</param>
    /// <param name="ip">The IP address of the service instance.</param>
    /// <param name="port">The port number of the service instance.</param>
    /// <param name="hostNum">The number of the host.</param>
    /// <param name="config">Config of the instance.</param>
    public Service(string serviceMgrIp, int serviceMgrPort, string name, string ip, int port, int hostNum, JToken config)
    {
        this.Config = config;

        this.serviceMgrConnection = new ImmediateServiceManagerConnectionOfService(
            serviceMgrIp,
            serviceMgrPort,
            checkServerStopped: () => this.stopFlag);

        this.serviceMgrConnection.RegisterMessageHandler(PackageType.ServiceManagerCommand, this.ServiceManagerCommandHandler);
        this.serviceMgrConnection.RegisterMessageHandler(PackageType.ServiceRpc, this.ServiceRpcHandler);
        this.serviceMgrConnection.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.ServiceRpcCallBackHandler);
        this.serviceMgrConnection.RegisterMessageHandler(PackageType.EntityRpcCallBack, this.EntityRpcCallBackHandler);

        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
    }

   /// <inheritdoc/>
    public void Loop()
    {
        Logger.Debug($"Service {this.Name} is running.");
        this.serviceMgrConnection.Run();

        Logger.Debug($"Service {this.Name} register self.");
        this.RegisterSelfToServiceManager();
        this.serviceMgrConnection.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.stopFlag = true;
        this.serviceMgrConnection.ShutDown();
    }

    private void ServiceRpcHandler(IMessage message)
    {
        var serviceRpc = (message as ServiceRpc)!;
        var serviceName = serviceRpc.ServiceName;
        var methodName = serviceRpc.MethodName;
        var shard = serviceRpc.ShardID;

        var service = this.serviceMap[serviceName][shard];
        service.EnqueueRpc(serviceRpc);
    }

    private uint GenerateConnectionId()
    {
        return this.acyncId++;
    }

    private void RegisterSelfToServiceManager()
    {
        var ready = new ServiceControl()
        {
            From = ServiceRemoteType.Service,
            Message = ServiceControlMessage.Ready,
        };
        ready.Args.Add(RpcHelper.GetRpcAny(this.Ip));
        ready.Args.Add(RpcHelper.GetRpcAny(this.Port));
        ready.Args.Add(RpcHelper.GetRpcAny(this.HostNum));

        Logger.Info($"Service {this.Name} notify ready.");
        this.serviceMgrConnection.Send(ready);
        this.waitForMailBox.Wait();
    }

    private void StartServiceInstance(ServiceManagerCommand serviceMgrCmd)
    {
        var arg = serviceMgrCmd.Args[0];
        var mb = RpcHelper.GetMailBox(arg);
        this.mailBox = RpcHelper.PbMailBoxToRpcMailBox(mb);
        Logger.Info($"Service {this.Name} is ready with mailbox {this.mailBox}.");
        this.waitForMailBox.Signal();
    }

    private void CreateNewServices(ServiceManagerCommand serviceMgrCmd)
    {
        var serviceDict = serviceMgrCmd.Args[0].Unpack<DictWithStringKeyArg>();
        foreach (var pair in serviceDict.PayLoad)
        {
            var serviceName = pair.Key;
            var serviceShardDict = pair.Value.Unpack<DictWithIntKeyArg>();

            foreach (var shard in serviceShardDict.PayLoad)
            {
                var shardNum = (uint)shard.Key;
                var shardId = RpcHelper.GetString(shard.Value);

                var mailbox = new Common.Rpc.MailBox(shardId, this.Ip, this.Port, this.HostNum);
                var service = ServiceHelper.CreateService(serviceName, shardNum, mailbox);
                service.OnSendServiceRpc = serviceRpc => this.serviceMgrConnection.Send(serviceRpc);
                service.OnSendServiceRpcCallBack = this.SendServiceRpcCallBack;
                service.OnSendEntityRpc = this.SendEntityRpc;
                if (!this.serviceMap.ContainsKey(serviceName))
                {
                    this.serviceMap[serviceName] = new Dictionary<uint, BaseService>();
                }

                this.serviceMap[serviceName][shardNum] = service;
                this.serviceMbMap[mailbox] = service;
                Logger.Info($"Start service {serviceName} shard {shardNum} with mailbox {mailbox}");

                service.Start().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception);
                        return;
                    }

                    var msg = new ServiceControl()
                    {
                        From = ServiceRemoteType.Service,
                        Message = ServiceControlMessage.ServiceReady,
                    };

                    msg.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(this.mailBox)));
                    msg.Args.Add(RpcHelper.GetRpcAny(serviceName));
                    msg.Args.Add(RpcHelper.GetRpcAny((int)shardNum));

                    Logger.Info($"Service shard {serviceName} {shardNum} notify ready.");
                    this.serviceMgrConnection.Send(msg);
                });
            }
        }
    }

    // service -> service mgr -> gate -> server -> entity
    // entity -> server -> gate -> service mgr -> service
    private void SendEntityRpc(EntityRpc rpc)
    {
        rpc.ServiceInstanceId = this.mailBox.Id;
        this.serviceMgrConnection.Send(rpc);
    }

    private void SendServiceRpcCallBack(ServiceRpcCallBack callback)
    {
        var rpcType = callback.RpcType;
        switch (rpcType)
        {
            case ServiceRpcType.ServiceToServer:
            case ServiceRpcType.ServiceToService:
            case ServiceRpcType.ServiceToHttp:
                this.serviceMgrConnection.Send(callback);
                break;
            case ServiceRpcType.HttpToService:
            case ServiceRpcType.ServerToService:
                throw new Exception($"Invalid rpc type {rpcType}.");
        }
    }

    private void NotifyAllServiceReady()
    {
        foreach (var dict in this.serviceMap.Values)
        {
            foreach (var service in dict.Values)
            {
                service.OnAllServiceReady().ContinueWith(t =>
                {
                    if (t.Exception is not null)
                    {
                        Logger.Error(t.Exception, "Service notify all service ready failed.");
                    }
                });
            }
        }
    }

    private void ServiceRpcCallBackHandler(IMessage message)
    {
        var callback = (ServiceRpcCallBack)message;
        var serviceMb = RpcHelper.PbMailBoxToRpcMailBox(callback.TargetMailBox);
        if (this.serviceMbMap.ContainsKey(serviceMb))
        {
            var service = this.serviceMbMap[serviceMb];
            service.OnServiceRpcCallBack(callback);
        }
        else
        {
            Logger.Warn($"Service {serviceMb} can not be found.");
        }
    }

    private void EntityRpcCallBackHandler(IMessage message)
    {
        var callback = (EntityRpcCallBack)message;

        var serviceMb = RpcHelper.PbMailBoxToRpcMailBox(callback.TargetMailBox);
        if (this.serviceMbMap.ContainsKey(serviceMb))
        {
            BaseService? service = this.serviceMbMap[serviceMb];
            service.OnEntityRpcCallBack(callback);
        }
        else
        {
            Logger.Warn($"Service {serviceMb} can not be found.");
        }
    }

    private void StopServiceInstance()
    {
        this.serviceMgrConnection.ShutDown();
    }

    private void ServiceManagerCommandHandler(IMessage message)
    {
        var serviceMgrCmd = (message as ServiceManagerCommand)!;

        switch (serviceMgrCmd.Type)
        {
            case ServiceManagerCommandType.Start:
                this.StartServiceInstance(serviceMgrCmd);
                break;
            case ServiceManagerCommandType.Stop:
                this.StopServiceInstance();
                break;
            case ServiceManagerCommandType.CreateNewServices:
                this.CreateNewServices(serviceMgrCmd);
                break;
            case ServiceManagerCommandType.Restart:
                throw new NotImplementedException();
            case ServiceManagerCommandType.AllServicesReady:
                this.NotifyAllServiceReady();
                break;
            default:
                break;
        }
    }
}