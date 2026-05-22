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
/// WebManager-facing endpoint for <see cref="Service"/>. Each Service host
/// process answers per-shard detail queries; only the host whose
/// <see cref="Service.serviceMap"/> contains <c>(serviceName, shard)</c>
/// publishes a reply.
/// </summary>
public partial class Service
{
    private WebMgrDispatcher webMgrDispatcher = null!;

    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager (service host).");
        this.webMgrDispatcher = new WebMgrDispatcher(this.Name, this.Name);
        this.webMgrDispatcher.ScanAndRegister(this);
        this.webMgrDispatcher.Init(Consts.RoutingKeyWebManagerToServiceHost);
    }

    [WebMgrHandler("getServiceShardDetailedInfo.webmgr.toServiceHost")]
    private JToken? HandleGetServiceShardDetailedInfo(JToken body)
    {
        var serviceName = body["serviceName"]!.ToString();
        var shard = body["shard"]!.ToObject<uint>();

        if (!this.serviceMap.TryGetValue(serviceName, out var shardMap)
            || !shardMap.TryGetValue(shard, out var baseService))
        {
            return null;
        }

        return this.BuildShardDetailedInfo(serviceName, baseService);
    }

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
