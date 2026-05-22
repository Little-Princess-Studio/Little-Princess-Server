// -----------------------------------------------------------------------
// <copyright file="Gate.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System.Linq;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;

/// <summary>
/// WebManager-facing endpoint for <see cref="Gate"/>. Wiring is declarative
/// via <see cref="WebMgrHandlerAttribute"/>; the shared
/// <see cref="WebMgrDispatcher"/> handles MQ bind / reply lookup / logging.
/// </summary>
public partial class Gate
{
    private WebMgrDispatcher webMgrDispatcher = null!;

    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager (gate).");
        this.webMgrDispatcher = new WebMgrDispatcher(this.Name, this.Name);
        this.webMgrDispatcher.ScanAndRegister(this);
        this.webMgrDispatcher.Init(Consts.RoutingKeyWebManagerToGate);
    }

    /// <summary>
    /// Reply only when this gate is the addressed one (id+hostNum match).
    /// Returning <c>null</c> tells the dispatcher to publish nothing - the
    /// WebManager will receive the reply from whichever gate owns the
    /// requested mailbox.
    /// </summary>
    [WebMgrHandler("getGateDetailedInfo.webmgr.toGate")]
    private JToken? HandleGetDetailedInfo(JToken body)
    {
        if (this.entity is null)
        {
            return null;
        }

        var gateId = body["gateId"]!.ToString();
        var hostNum = body["hostNum"]!.ToObject<int>();
        if (gateId != this.entity.MailBox.Id || hostNum != this.entity.MailBox.HostNum)
        {
            return null;
        }

        return this.BuildDetailedInfo();
    }

    private JObject BuildDetailedInfo()
    {
        var serverConnections = new JArray(
            (this.tcpClientsToServer ?? Enumerable.Empty<LPS.Server.Rpc.TcpClient>())
                .Select(c => new JObject
                {
                    ["id"] = c.MailBox.Id,
                    ["ip"] = c.MailBox.Ip,
                    ["port"] = c.MailBox.Port,
                    ["hostNum"] = c.MailBox.HostNum,
                }));

        var gateConnections = new JArray(
            (this.tcpClientsToOtherGate ?? Enumerable.Empty<LPS.Server.Rpc.TcpClient>())
                .Select(c => new JObject
                {
                    ["id"] = c.MailBox.Id,
                    ["ip"] = c.MailBox.Ip,
                    ["port"] = c.MailBox.Port,
                    ["hostNum"] = c.MailBox.HostNum,
                }));

        var clientEntities = new JArray(
            this.entityIdToClientConnMapping.Select(kv => new JObject
            {
                ["entityId"] = kv.Key,
                ["mailbox"] = new JObject
                {
                    ["id"] = kv.Value.MailBox.Id,
                    ["ip"] = kv.Value.MailBox.Ip,
                    ["port"] = kv.Value.MailBox.Port,
                    ["hostNum"] = kv.Value.MailBox.HostNum,
                },
            }));

        return new JObject
        {
            ["name"] = this.Name,
            ["mailbox"] = new JObject
            {
                ["id"] = this.entity!.MailBox.Id,
                ["ip"] = this.Ip,
                ["port"] = this.Port,
                ["hostNum"] = this.HostNum,
            },
            ["serviceManager"] = new JObject
            {
                ["id"] = this.serviceManagerMailBox.Id ?? string.Empty,
                ["ip"] = this.serviceManagerMailBox.Ip ?? string.Empty,
                ["port"] = this.serviceManagerMailBox.Port,
                ["hostNum"] = this.serviceManagerMailBox.HostNum,
            },
            ["counters"] = new JObject
            {
                ["serverConnections"] = serverConnections.Count,
                ["gateConnections"] = gateConnections.Count,
                ["clientEntities"] = clientEntities.Count,
                ["pendingClientAuths"] = this.createEntityMapping.Count,
                ["sendQueueDepth"] = this.sendQueue.Count,
                ["readyToPumpClients"] = this.readyToPumpClients,
            },
            ["serverConnections"] = serverConnections,
            ["gateConnections"] = gateConnections,
            ["clientEntities"] = clientEntities,
        };
    }
}
