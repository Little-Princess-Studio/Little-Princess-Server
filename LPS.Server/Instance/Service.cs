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
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server;
using LPS.Server.Instance.HostConnection;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Rpc.InnerMessages;
using LPS.Server.Service;

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

    private readonly CountdownEvent waitForMailBox = new(1);

    private readonly Dictionary<string, Dictionary<uint, ServiceBase>> serviceMap = new();

    private Common.Rpc.MailBox mailBox;

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

        this.serviceMgrConnection.RegisterMessageHandler(PackageType.ServiceManagerCommand, this.ServiceManagerCommandHandler);

        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
    }

    /// <inheritdoc/>
    public void Loop()
    {
        this.serviceMgrConnection.Run();
        this.RegisterSelfToServiceManager();
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

        this.serviceMgrConnection.Send(ready);
        this.waitForMailBox.Wait();
    }

    private void StartServiceInstance(ServiceManagerCommand serviceMgrCmd)
    {
        var arg = serviceMgrCmd.Args[0];
        var mb = arg.Unpack<Common.Rpc.InnerMessages.MailBox>();
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
            var serviceShards = pair.Value.Unpack<ListArg>();

            foreach (var shard in serviceShards.PayLoad)
            {
                var shardNum = (uint)shard.Unpack<IntArg>().PayLoad;
                var service = ServiceHelper.CreateService(serviceName, shardNum);
                if (!this.serviceMap.ContainsKey(serviceName))
                {
                    this.serviceMap[serviceName] = new Dictionary<uint, ServiceBase>();
                }

                this.serviceMap[serviceName][shardNum] = service;

                Logger.Info($"Start service {serviceName} shard {shardNum}");
                service.Start();
            }
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
            default:
                break;
        }
    }
}