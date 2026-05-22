// -----------------------------------------------------------------------
// <copyright file="HostManager.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System.Linq;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;

/// <summary>
/// HostManager watches the lifecycle of every Server/Gate/ServiceManager
/// and is queryable by the WebManager via the declarative
/// <see cref="WebMgrDispatcher"/>.
/// </summary>
public partial class HostManager : IInstance
{
    private WebMgrDispatcher webMgrDispatcher = null!;

    private void InitMessageQueueClientToWebManager()
    {
        Logger.Debug("Start mq client for web manager (hostmanager).");
        this.webMgrDispatcher = new WebMgrDispatcher(this.Name, this.Name);
        this.webMgrDispatcher.ScanAndRegister(this);
        this.webMgrDispatcher.Init(Consts.RoutingKeyToHostManager);
    }

    [WebMgrHandler("getServerBasicInfo.toHostMgr")]
    private JToken HandleGetServerBasicInfo(JToken body)
    {
        _ = body;
        return new JObject
        {
            ["serverCnt"] = this.DesiredServerNum,
            ["serverMailBoxes"] = new JArray(this.serversMailBoxes.Select(conn => new JObject
            {
                ["id"] = conn.Id,
                ["ip"] = conn.Ip,
                ["port"] = conn.Port,
                ["hostNum"] = conn.HostNum,
            })),
        };
    }

    [WebMgrHandler("getServerPingPongInfo.toHostMgr")]
    private JToken HandleGetServerPingPongInfo(JToken body)
    {
        _ = body;
        return new JObject
        {
            ["srvPingPongInfo"] = new JArray(
                this.serversMailBoxes
                    .Where(mb => this.instanceStatusManager.HasInstance(mb))
                    .Select(mb => this.instanceStatusManager.GetStatus(mb))
                    .Select(status => new JObject
                    {
                        ["id"] = status.MailBox.Id,
                        ["status"] = (int)status.Status,
                    })),
        };
    }

    [WebMgrHandler("getClusterOverview.toHostMgr")]
    private JToken HandleGetClusterOverview(JToken body)
    {
        _ = body;
        return this.BuildClusterOverview();
    }

    /// <summary>
    /// Snapshot every instance the HostManager has registered, grouped by role.
    /// </summary>
    private JObject BuildClusterOverview()
    {
        var byType = this.instanceStatusManager.Snapshot()
            .GroupBy(s => s.InstanceType.ToString())
            .ToDictionary(g => g.Key, g => g.ToList());

        JArray ToArray(string key) => byType.TryGetValue(key, out var list)
            ? new JArray(list.Select(s => new JObject
            {
                ["id"] = s.MailBox.Id,
                ["ip"] = s.MailBox.Ip,
                ["port"] = s.MailBox.Port,
                ["hostNum"] = s.MailBox.HostNum,
                ["status"] = (int)s.Status,
                ["lastHeartBeat"] = s.LastHeartBeat.ToString("O"),
            }))
            : new JArray();

        return new JObject
        {
            ["hostManager"] = new JObject
            {
                ["ip"] = this.Ip,
                ["port"] = this.Port,
                ["hostNum"] = this.HostNum,
                ["desiredServerNum"] = this.DesiredServerNum,
                ["desiredGateNum"] = this.DesiredGateNum,
                ["status"] = this.Status.ToString(),
            },
            ["gates"] = ToArray(InstanceType.Gate.ToString()),
            ["servers"] = ToArray(InstanceType.Server.ToString()),
            ["serviceManagers"] = ToArray(InstanceType.ServiceManager.ToString()),
            ["services"] = ToArray(InstanceType.Service.ToString()),
        };
    }
}
