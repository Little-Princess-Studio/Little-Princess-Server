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
/// WebManager-facing message-queue endpoint for <see cref="Gate"/>.
/// Mirrors <see cref="Server.WebManager.cs"/>: a dedicated MQ client bound
/// to <see cref="Consts.WebMgrExchangeName"/> with the
/// <see cref="Consts.RoutingKeyWebManagerToGate"/> filter. Replies are
/// guarded by gateId+hostNum so other gates ignore the request.
/// </summary>
public partial class Gate
{
    private MessageQueueClient messageQueueClientToWebMgr = null!;

    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager (gate).");
        this.messageQueueClientToWebMgr = new MessageQueueClient();
        this.messageQueueClientToWebMgr.Init();
        this.messageQueueClientToWebMgr.AsProducer();
        this.messageQueueClientToWebMgr.AsConsumer();

        this.messageQueueClientToWebMgr.DeclareExchange(Consts.WebMgrExchangeName);
        this.messageQueueClientToWebMgr.DeclareExchange(Consts.ServerExchangeName);
        this.messageQueueClientToWebMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.Name),
            Consts.WebMgrExchangeName,
            Consts.RoutingKeyWebManagerToGate);
        this.messageQueueClientToWebMgr.Observe(
            Consts.GenerateWebManagerQueueName(this.Name),
            this.HandleWebMgrMqMessage);
    }

    private void HandleWebMgrMqMessage(string msg, string routingKey)
    {
        if (routingKey != Consts.GetGateDetailedInfo)
        {
            return;
        }

        // Gate entity may not yet be created when the request arrives during
        // very early startup - bail quietly so the operator sees a clean
        // "no such gate" timeout rather than a NRE in the dispatcher.
        if (this.entity is null)
        {
            return;
        }

        var (msgId, json) = MessageQueueJsonBody.From(msg);
        var gateId = json["gateId"]!.ToString();
        var hostNum = json["hostNum"]!.ToObject<int>();
        if (gateId != this.entity.MailBox.Id || hostNum != this.entity.MailBox.HostNum)
        {
            return;
        }

        var res = MessageQueueJsonBody.Create(msgId, this.BuildDetailedInfo());
        this.messageQueueClientToWebMgr.Publish(
            res.ToJson(),
            Consts.ServerExchangeName,
            Consts.GateDetailedInfoRes);
    }

    /// <summary>
    /// Snapshot the live Gate state into a JSON object. Shape matches the
    /// /api/web-manager/gate-detailed-info contract on the WebManager side.
    /// </summary>
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
