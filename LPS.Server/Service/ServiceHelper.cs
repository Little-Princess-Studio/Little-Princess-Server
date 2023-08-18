// -----------------------------------------------------------------------
// <copyright file="ServiceHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Service;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Util;

/// <summary>
/// Helper class for services.
/// </summary>
internal static class ServiceHelper
{
    private static Dictionary<string, Type> serviceMap = null!;

    /// <summary>
    /// Scans for services in the specified namespace and assemblies, and initializes them.
    /// </summary>
    /// <param name="namespace">The namespace to scan for services.</param>
    /// <param name="assemblies">The assemblies to scan for services. If null, all loaded assemblies will be scanned.</param>
    public static void ScanServices(string @namespace, Assembly[]? assemblies = null)
    {
        var types = AttributeHelper.ScanTypeWithNamespaceAndAttribute(
            @namespace,
            typeof(ServiceAttribute),
            false,
            type => type.IsClass && type.IsSubclassOf(typeof(ServiceBase)),
            assemblies);
        serviceMap = types.ToDictionary(type => type.GetCustomAttribute<ServiceAttribute>()!.ServiceName, type => type);
    }

    /// <summary>
    /// Creates a new instance of the service.
    /// </summary>
    /// <param name="serviceName">The name of the service to create.</param>
    /// <param name="shard">The shard to assign to the service.</param>
    /// <returns>A new instance of the service.</returns>
    public static ServiceBase CreateService(string serviceName, uint shard)
    {
        if (serviceMap.ContainsKey(serviceName))
        {
            var service = (ServiceBase)Activator.CreateInstance(serviceMap[serviceName])!;
            service.Shard = shard;
            return service;
        }
        else
        {
            throw new ArgumentException($"Service {serviceName} not found.");
        }
    }

    /// <summary>
    /// Assigns services to service instances based on their shard count.
    /// </summary>
    /// <param name="serviceInstanceCnt">The number of service instances to assign services to.</param>
    /// <returns>A list of dictionaries, where each dictionary contains the assigned services and their corresponding shards.</returns>
    public static List<Dictionary<string, List<int>>> AssignServicesToServiceInstances(int serviceInstanceCnt)
    {
        var resultList = new List<Dictionary<string, List<int>>>(serviceInstanceCnt);
        for (int i = 0; i < serviceInstanceCnt; ++i)
        {
            resultList.Add(new Dictionary<string, List<int>>());
        }

        var prevSlotIdx = -1;
        foreach (var (serviceName, serviceType) in serviceMap)
        {
            var shardCnt = serviceType.GetCustomAttribute<ServiceAttribute>()!.DefaultShardCount;
            for (int shard = 0; shard < shardCnt; ++shard)
            {
                var slotIdx = (shard + prevSlotIdx + 1) % serviceInstanceCnt;
                var slot = resultList[slotIdx];

                if (slot.ContainsKey(serviceName))
                {
                    slot[serviceName].Add(shard);
                }
                else
                {
                    slot[serviceName] = new List<int> { shard };
                }

                prevSlotIdx = slotIdx;
            }
        }

        return resultList;
    }

    /// <summary>
    /// Calls the specified service with the given RPC request.
    /// </summary>
    /// <param name="service">The service to call.</param>
    /// <param name="serviceRpc">The RPC request to send to the service.</param>
    public static void CallService(ServiceBase service, ServiceRpc serviceRpc)
    {
        // todo: impl jit to compile methodInfo.invoke to expression.invoke to improve perf.
        var descriptor = RpcHelper.GetRpcMethodArgTypes(service.TypeId, rpcMethodName: serviceRpc.MethodName);
        var authority = descriptor.Authority;
        var methodInfo = descriptor.Method;

        var args = RpcHelper.ProtobufArgsToRpcArgList(serviceRpc.Args, methodInfo);

        object? res;
        try
        {
            res = methodInfo.Invoke(service, args);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to call rpc method.");
            return;
        }

        bool notifyOnly = serviceRpc.NotifyOnly;

        var senderMailBox = serviceRpc.SenderMailBox;

        var sendRpcType = serviceRpc.RpcType;
        if (serviceRpc.RpcType == ServiceRpcType.ClientToService)
        {
            Logger.Info("rpc call is from client, the result will be sent to client.");
            sendRpcType = ServiceRpcType.ServiceToClient;
        }
        else if (serviceRpc.RpcType == ServiceRpcType.ServiceToClient)
        {
            sendRpcType = ServiceRpcType.ClientToService;
        }
    }
}