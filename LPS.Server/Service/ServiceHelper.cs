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
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Util;

/// <summary>
/// Helper class for services.
/// </summary>
internal static class ServiceHelper
{
    private static Dictionary<string, System.Type> serviceMap = null!;

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
            type => type.IsClass && type.IsSubclassOf(typeof(BaseService)),
            assemblies);
        serviceMap = types.ToDictionary(type => type.GetCustomAttribute<ServiceAttribute>()!.ServiceName, type => type);
    }

    /// <summary>
    /// Scans all the RPC methods in the specified namespace and registers them.
    /// </summary>
    /// <param name="namespaceNames">The namespaces to scan for RPC methods.</param>
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    /// <exception cref="Exception">Thrown if there is an error while scanning or registering the RPC methods.</exception>
    public static void ScanRpcMethods(string[] namespaceNames, Assembly[]? extraAssemblies = null) => RpcHelper.ScanRpcMethods(
            namespaceNames,
            typeof(BaseService),
            typeof(ServiceAttribute),
            type => type.GetCustomAttribute<ServiceAttribute>()!.ServiceName,
            extraAssemblies);

    /// <summary>
    /// Creates a new instance of the service.
    /// </summary>
    /// <param name="serviceName">The name of the service to create.</param>
    /// <param name="shard">The shard to assign to the service.</param>
    /// <param name="mailBox">The mailbox of the service.</param>
    /// <returns>A new instance of the service.</returns>
    public static BaseService CreateService(string serviceName, uint shard, Common.Rpc.MailBox mailBox)
    {
        if (serviceMap.ContainsKey(serviceName))
        {
            var service = (BaseService)Activator.CreateInstance(serviceMap[serviceName])!;
            service.Shard = shard;
            service.MailBox = mailBox;
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
    public static void CallService(BaseService service, ServiceRpc serviceRpc)
    {
        // todo: impl jit to compile methodInfo.invoke to expression.invoke to improve perf.
        var descriptor = RpcHelper.GetRpcMethodArgTypes(service.TypeId, serviceRpc.MethodName);
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

        ServiceRpcType sendRpcType;

        switch (serviceRpc.RpcType)
        {
            case ServiceRpcType.ServerToService:
                sendRpcType = ServiceRpcType.ServiceToServer;
                break;
            case ServiceRpcType.ServiceToService:
                sendRpcType = ServiceRpcType.ServiceToService;
                break;
            case ServiceRpcType.HttpToService:
                sendRpcType = ServiceRpcType.ServiceToHttp;
                break;
            case ServiceRpcType.ServiceToServer:
            case ServiceRpcType.ServiceToHttp:
            default:
                throw new Exception($"Invalid rpc type {serviceRpc.RpcType}.");
        }

        if (res != null)
        {
            HandleRpcMethodResult(service, serviceRpc, methodInfo, res, senderMailBox, sendRpcType, notifyOnly);
        }
        else
        {
            if (!notifyOnly)
            {
                service.SendCallBackWithRpcId(
                    serviceRpc.RpcID,
                    serviceRpc.ServiceManagerRpcId,
                    RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                    sendRpcType,
                    res);
            }
        }
    }

    /// <summary>
    /// Build service RPC message.
    /// </summary>
    /// <param name="rpcId">RPC id.</param>
    /// <param name="targetMailBox">Target mailbox.</param>
    /// <param name="rpcType">RPC type.</param>
    /// <param name="serviceManagerRpcId">Service manager rpc ID.</param>
    /// <param name="result">RPC callback result.</param>
    /// <returns>RPC protobuf object.</returns>
    public static ServiceRpcCallBack BuildServiceRpcCallBackMessage(
        uint rpcId,
        Common.Rpc.MailBox? targetMailBox,
        ServiceRpcType rpcType,
        uint serviceManagerRpcId,
        object? result)
    {
        var rpc = new ServiceRpcCallBack
        {
            RpcID = rpcId,
            RpcType = rpcType,
            ServiceManagerRpcId = serviceManagerRpcId,
            TargetMailBox = targetMailBox is not null ?
                RpcHelper.RpcMailBoxToPbMailBox((Common.Rpc.MailBox)targetMailBox) : null,
        };

        rpc.Result = Any.Pack(RpcHelper.RpcArgToProtoBuf(result));

        return rpc;
    }

    private static void HandleRpcMethodResult(
        BaseService service,
        ServiceRpc serviceRpc,
        MethodInfo methodInfo,
        object res,
        Common.Rpc.InnerMessages.MailBox senderMailBox,
        ServiceRpcType sendRpcType,
        bool notifyOnly)
    {
        var returnType = methodInfo.ReturnType;
        if (returnType.IsGenericType)
        {
            // TODO: for performance, need using IL instead of dynamic/reflection?
            if (returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                SendTaskResult(service, serviceRpc, senderMailBox, sendRpcType, res, notifyOnly);
            }
            else if (returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                SendValueTaskResult(service, serviceRpc, senderMailBox, sendRpcType, res, notifyOnly);
            }
        }
        else if (returnType == typeof(Task))
        {
            ((Task)res).ContinueWith(task =>
            {
                if (task.Exception is not null)
                {
                    Logger.Error(task.Exception, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                service.SendCallBackWithRpcId(
                    serviceRpc.RpcID,
                    serviceRpc.ServiceManagerRpcId,
                    RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                    sendRpcType,
                    null);
            });
        }
        else if (returnType == typeof(ValueTask))
        {
            var task = (ValueTask)res;
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var e = task.AsTask().Exception!;
                    Logger.Error(e, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                service.SendCallBackWithRpcId(
                    serviceRpc.RpcID,
                    serviceRpc.ServiceManagerRpcId,
                    RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                    sendRpcType,
                    null);
            }
            else
            {
                // if ValueTask not complete, alloc awaiter to wait
                task.AsTask().ContinueWith((t) =>
                {
                    if (t.Exception is not null)
                    {
                        Logger.Error(t.Exception, "Failed to call rpc method.");
                        return;
                    }

                    if (notifyOnly)
                    {
                        return;
                    }

                    service.SendCallBackWithRpcId(
                        serviceRpc.RpcID,
                        serviceRpc.ServiceManagerRpcId,
                        RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                        sendRpcType,
                        null);
                });
            }
        }
    }

    private static void SendValueTaskResult(
        BaseService service,
        ServiceRpc serviceRpc,
        Common.Rpc.InnerMessages.MailBox senderMailBox,
        ServiceRpcType sendRpcType,
        in object res,
        bool notifyOnly)
    {
        void SendDynamic(dynamic t) =>
            service.SendCallBackWithRpcId(
                serviceRpc.RpcID,
                serviceRpc.ServiceManagerRpcId,
                RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);

        void Send<T>(in ValueTask<T> t) => service.SendCallBackWithRpcId(
                serviceRpc.RpcID,
                serviceRpc.ServiceManagerRpcId,
                RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);

        void HandleValueTask<T>(in ValueTask<T> task)
        {
            // ValueTask should always be sync
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var e = task.AsTask().Exception!;
                    Logger.Error(e, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                Send(task);
            }
            else
            {
                // if ValueTask not complete, alloc awaiter to wait
                task.AsTask().ContinueWith((t) =>
                {
                    if (t.Exception is not null)
                    {
                        Logger.Error(t.Exception, "Failed to call rpc method.");
                        return;
                    }

                    if (notifyOnly)
                    {
                        return;
                    }

                    service.SendCallBackWithRpcId(
                        serviceRpc.RpcID,
                        serviceRpc.ServiceManagerRpcId,
                        RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                        sendRpcType,
                        t.Result);
                });
            }
        }

        void HandleValueTaskDynamic(dynamic task)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var e = task.AsTask().Exception!;
                    Logger.Error(e, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                SendDynamic(task);
            }
            else
            {
                task.AsTask().ContinueWith(new Action(() =>
                {
                    if (task.Exception is not null)
                    {
                        Logger.Error(task.Exception, "Failed to call rpc method.");
                        return;
                    }

                    if (notifyOnly)
                    {
                        return;
                    }

                    service.SendCallBackWithRpcId(
                        serviceRpc.RpcID,
                        serviceRpc.ServiceManagerRpcId,
                        RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                        sendRpcType,
                        task.Result);
                }));
            }
        }

        switch (res)
        {
            case ValueTask<int> task:
                HandleValueTask(task);
                break;
            case ValueTask<string> task:
                HandleValueTask(task);
                break;
            case ValueTask<float> task:
                HandleValueTask(task);
                break;
            case ValueTask<LPS.Common.Rpc.MailBox> task:
                HandleValueTask(task);
                break;
            case ValueTask<bool> task:
                HandleValueTask(task);
                break;
            default:
                {
                    dynamic task = res;
                    HandleValueTaskDynamic(task);
                }

                break;
        }
    }

    private static void SendTaskResult(
        BaseService service,
        ServiceRpc serviceRpc,
        LPS.Common.Rpc.InnerMessages.MailBox senderMailBox,
        ServiceRpcType sendRpcType,
        in object res,
        bool notifyOnly)
    {
        void SendDynamic(dynamic t)
        {
            if (t.Exception is not null)
            {
                Logger.Error(t.Exception, "Failed to call rpc method.");
                return;
            }

            if (notifyOnly)
            {
                return;
            }

            service.SendCallBackWithRpcId(
                serviceRpc.RpcID,
                serviceRpc.ServiceManagerRpcId,
                RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);
        }

        void Send<T>(Task<T> t)
        {
            if (t.Exception is not null)
            {
                Logger.Error(t.Exception, "Failed to call rpc method.");
                return;
            }

            if (notifyOnly)
            {
                return;
            }

            service.SendCallBackWithRpcId(
                serviceRpc.RpcID,
                serviceRpc.ServiceManagerRpcId,
                RpcHelper.PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);
        }

        switch (res)
        {
            case Task<int> task:
                task.ContinueWith(Send);
                break;
            case Task<string> task:
                task.ContinueWith(Send);
                break;
            case Task<float> task:
                task.ContinueWith(Send);
                break;
            case Task<Common.Rpc.MailBox> task:
                task.ContinueWith(Send);
                break;
            case Task<bool> task:
                task.ContinueWith(Send);
                break;
            default:
                {
                    dynamic task = res;
                    task.ContinueWith((Action<dynamic>)SendDynamic);
                }

                break;
        }
    }
}