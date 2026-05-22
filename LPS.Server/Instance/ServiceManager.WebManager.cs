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
/// WebManager endpoint for <see cref="ServiceManager"/>. Owns its dedicated
/// MQ client via <see cref="WebMgrDispatcher"/>.
/// </summary>
public partial class ServiceManager
{
    private WebMgrDispatcher webMgrDispatcher = null!;

    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager (servicemanager).");
        this.webMgrDispatcher = new WebMgrDispatcher(this.Name, this.Name);
        this.webMgrDispatcher.ScanAndRegister(this);
        this.webMgrDispatcher.Init(Consts.RoutingKeyToServiceMgr);
    }

    [WebMgrHandler("getServiceList.toServiceMgr")]
    private JToken HandleGetServiceList(JToken body)
    {
        _ = body;
        return this.BuildServiceList();
    }

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
