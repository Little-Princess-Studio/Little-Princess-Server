// -----------------------------------------------------------------------
// <copyright file="Service.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System.Linq;
using System.Reflection;
using LPS.Common.Debug;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.MessageQueue;
using LPS.Server.Service;
using Newtonsoft.Json.Linq;

/// <summary>
/// WebManager-facing message-queue endpoint for <see cref="Service"/>.
/// Mirrors <see cref="Server.WebManager.cs"/>: each Service host process
/// binds its own queue on <see cref="Consts.WebMgrExchangeName"/> with the
/// <see cref="Consts.RoutingKeyWebManagerToServiceHost"/> filter. Only the
/// host that owns the requested {serviceName, shard} replies.
/// </summary>
public partial class Service
{
    private MessageQueueClient messageQueueClientToWebMgr = null!;

    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager (service host).");
        this.messageQueueClientToWebMgr = new MessageQueueClient();
        this.messageQueueClientToWebMgr.Init();
        this.messageQueueClientToWebMgr.AsProducer();
        this.messageQueueClientToWebMgr.AsConsumer();

        this.messageQueueClientToWebMgr.DeclareExchange(Consts.WebMgrExchangeName);
        this.messageQueueClientToWebMgr.DeclareExchange(Consts.ServerExchangeName);
        this.messageQueueClientToWebMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.Name),
            Consts.WebMgrExchangeName,
            Consts.RoutingKeyWebManagerToServiceHost);
        this.messageQueueClientToWebMgr.Observe(
            Consts.GenerateWebManagerQueueName(this.Name),
            this.HandleWebMgrMqMessage);
    }

    private void HandleWebMgrMqMessage(string msg, string routingKey)
    {
        if (routingKey != Consts.GetServiceShardDetailedInfo)
        {
            return;
        }

        var (msgId, json) = MessageQueueJsonBody.From(msg);
        var serviceName = json["serviceName"]!.ToString();
        var shard = json["shard"]!.ToObject<uint>();

        // Only the host that actually owns this shard answers; everyone else
        // stays silent (broadcast fan-out is by design).
        if (!this.serviceMap.TryGetValue(serviceName, out var shardMap)
            || !shardMap.TryGetValue(shard, out var baseService))
        {
            return;
        }

        var res = MessageQueueJsonBody.Create(msgId, this.BuildShardDetailedInfo(serviceName, baseService));
        this.messageQueueClientToWebMgr.Publish(
            res.ToJson(),
            Consts.ServerExchangeName,
            Consts.ServiceShardDetailedInfoRes);
    }

    /// <summary>
    /// Snapshot a single shard's identity + reflected RPC surface. Live
    /// state inside the concrete service is not exposed yet - that would
    /// require a virtual hook on <see cref="BaseService"/>.
    /// </summary>
    private JObject BuildShardDetailedInfo(string serviceName, BaseService baseService)
    {
        var serviceType = baseService.GetType();
        var rpcMethods = new JArray(
            serviceType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<RpcMethodAttribute>() is not null)
                .Select(m => new JObject
                {
                    ["name"] = m.Name,
                    ["authority"] = m.GetCustomAttribute<RpcMethodAttribute>()!.Authority.ToString(),
                    ["returnType"] = m.ReturnType.Name,
                    ["parameters"] = new JArray(m.GetParameters().Select(p => new JObject
                    {
                        ["name"] = p.Name ?? string.Empty,
                        ["type"] = p.ParameterType.Name,
                    })),
                }));

        return new JObject
        {
            ["serviceName"] = serviceName,
            ["serviceClass"] = serviceType.Name,
            ["shard"] = baseService.Shard,
            ["typeId"] = baseService.TypeId,
            ["shardMailbox"] = new JObject
            {
                ["id"] = baseService.MailBox.Id,
                ["ip"] = baseService.MailBox.Ip,
                ["port"] = baseService.MailBox.Port,
                ["hostNum"] = baseService.MailBox.HostNum,
            },
            ["hostMailbox"] = new JObject
            {
                ["name"] = this.Name,
                ["ip"] = this.Ip,
                ["port"] = this.Port,
                ["hostNum"] = this.HostNum,
            },
            ["coLocatedShards"] = new JArray(
                this.serviceMap.SelectMany(kv => kv.Value.Select(s => new JObject
                {
                    ["serviceName"] = kv.Key,
                    ["shard"] = s.Key,
                    ["shardId"] = s.Value.MailBox.Id,
                }))),
            ["rpcMethods"] = rpcMethods,
        };
    }
}
