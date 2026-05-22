// -----------------------------------------------------------------------
// <copyright file="ServiceManager.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System.Linq;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;

/// <summary>
/// WebManager-facing message-queue endpoint for <see cref="ServiceManager"/>.
/// Mirrors <see cref="Server.WebManager.cs"/>: a dedicated MQ client is bound
/// to <see cref="Consts.WebMgrExchangeName"/> with the
/// <see cref="Consts.RoutingKeyToServiceMgr"/> filter so the WebManager can
/// poll the live service routing map without sharing a queue with any other
/// instance kind.
/// </summary>
public partial class ServiceManager
{
    private readonly MessageQueueClient messageQueueClientToWebMgr = new();

    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager.");
        this.messageQueueClientToWebMgr.Init();
        this.messageQueueClientToWebMgr.AsProducer();
        this.messageQueueClientToWebMgr.AsConsumer();

        this.messageQueueClientToWebMgr.DeclareExchange(Consts.WebMgrExchangeName);
        this.messageQueueClientToWebMgr.DeclareExchange(Consts.ServerExchangeName);

        // The queue is name-scoped to this instance (mirror Server/HostManager
        // pattern) so multiple ServiceManagers in the future do not steal
        // each other's WebManager requests.
        this.messageQueueClientToWebMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.Name),
            Consts.WebMgrExchangeName,
            Consts.RoutingKeyToServiceMgr);

        this.messageQueueClientToWebMgr.Observe(
            Consts.GenerateWebManagerQueueName(this.Name),
            this.HandleWebMgrMqMessage);
    }

    private void HandleWebMgrMqMessage(string msg, string routingKey)
    {
        if (routingKey != Consts.GetServiceList)
        {
            return;
        }

        var (msgId, _) = MessageQueueJsonBody.From(msg);
        var res = MessageQueueJsonBody.Create(msgId, this.BuildServiceList());

        this.messageQueueClientToWebMgr.Publish(
            res.ToJson(),
            Consts.ServerExchangeName,
            Consts.GetServiceListRes);
    }

    /// <summary>
    /// Serialise the current <see cref="serviceRoutingMap"/> to a JSON object
    /// the WebManager can render directly. Shape matches the documented
    /// /api/web-manager/services-roster contract:
    /// {
    ///   serviceManager: { ip, port, hostNum },
    ///   services: [ { name, shardCount, allShardReady, unreadyShards: [int],
    ///                 shards: [ { shard, id, ip, port, hostNum } ] } ]
    /// }.
    /// </summary>
    private JObject BuildServiceList()
    {
        var services = new JArray();

        foreach (var (serviceName, desc) in this.serviceRoutingMap)
        {
            var shards = new JArray(
                desc.SnapshotShards()
                    .OrderBy(kv => kv.Key)
                    .Select(kv => new JObject
                    {
                        ["shard"] = kv.Key,
                        ["id"] = kv.Value.Id,
                        ["ip"] = kv.Value.Ip,
                        ["port"] = kv.Value.Port,
                        ["hostNum"] = kv.Value.HostNum,
                    }));

            var unready = new JArray(desc.SnapshotUnreadyShards().OrderBy(x => x));

            services.Add(new JObject
            {
                ["name"] = serviceName,
                ["shardCount"] = desc.ShardCount,
                ["allShardReady"] = desc.AllShardReady,
                ["unreadyShards"] = unready,
                ["shards"] = shards,
            });
        }

        return new JObject
        {
            ["serviceManager"] = new JObject
            {
                ["name"] = this.Name,
                ["ip"] = this.Ip,
                ["port"] = this.Port,
                ["hostNum"] = this.HostNum,
            },
            ["services"] = services,
        };
    }
}
