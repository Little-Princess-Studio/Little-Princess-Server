// -----------------------------------------------------------------------
// <copyright file="Server.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;

/// <summary>
/// Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
/// redirect to target server instance.
/// </summary>
public partial class Server
{
    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager.");
        this.messageQueueClientToWebMgr = new MessageQueueClient();
        this.messageQueueClientToWebMgr.Init();
        this.messageQueueClientToWebMgr.AsProducer();
        this.messageQueueClientToWebMgr.AsConsumer();

        this.messageQueueClientToWebMgr.DeclareExchange(Consts.WebMgrExchangeName);
        this.messageQueueClientToWebMgr.DeclareExchange(Consts.ServerExchangeName);
        this.messageQueueClientToWebMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.Name),
            Consts.WebMgrExchangeName,
            Consts.RoutingKeyWebManagerToServer);
        this.messageQueueClientToWebMgr.Observe(
            Consts.GenerateWebManagerQueueName(this.Name),
            this.HandleWebMgrMqMessage);
    }

    private void HandleWebMgrMqMessage(string msg, string routingKey)
    {
        Logger.Debug($"Msg received from web mgr: {msg}, routingKey: {routingKey}");
        if (routingKey == Consts.GetServerDetailedInfo)
        {
            var (msgId, json) = MessageQueueJsonBody.From(msg);

            var serverId = json["serverId"]!.ToString();
            var hostNum = json["hostNum"]!.ToObject<int>();

            if (serverId != this.entity!.MailBox.Id || hostNum != this.entity!.MailBox.HostNum)
            {
                return;
            }

            var res = MessageQueueJsonBody.Create(
                msgId,
                new JObject
                {
                    ["name"] = this.Name,
                    ["mailbox"] = new JObject
                    {
                        ["id"] = this.entity!.MailBox.Id,
                        ["ip"] = this.Ip,
                        ["port"] = this.Port,
                        ["hostNum"] = this.HostNum,
                    },
                    ["entitiesCnt"] = this.localEntityDict.Count,
                    ["cellCnt"] = this.cells.Count,
                });
            this.messageQueueClientToWebMgr!.Publish(
                res.ToJson(),
                Consts.ServerExchangeName,
                Consts.ServerDetailedInfo);
        }
        else if (routingKey == Consts.GetAllEntitiesOfServer)
        {
            var (msgId, json) = MessageQueueJsonBody.From(msg);

            var serverId = json["serverId"]!.ToString();
            var hostNum = json["hostNum"]!.ToObject<int>();

            Logger.Debug(
                $"GetAllEntitiesOfServer {serverId} {hostNum} {this.entity!.MailBox.Id} {this.entity!.MailBox.HostNum}");
            if (serverId != this.entity!.MailBox.Id || hostNum != this.entity!.MailBox.HostNum)
            {
                return;
            }

            var entities = new JArray();

            foreach (var (_, distributeEntity) in this.localEntityDict)
            {
                entities.Add(new JObject
                {
                    ["id"] = distributeEntity.MailBox.Id,
                    ["mailbox"] = new JObject
                    {
                        ["id"] = distributeEntity.MailBox.Id,
                        ["ip"] = distributeEntity.MailBox.Ip,
                        ["port"] = distributeEntity.MailBox.Port,
                        ["hostNum"] = distributeEntity.MailBox.HostNum,
                    },
                    ["entityClassName"] = distributeEntity.GetType().Name,
                    ["cellEntityId"] = distributeEntity.Cell.MailBox.Id,
                });
            }

            foreach (var (_, cellEntity) in this.cells)
            {
                entities.Add(new JObject
                {
                    ["id"] = cellEntity.MailBox.Id,
                    ["mailbox"] = new JObject()
                    {
                        ["id"] = cellEntity.MailBox.Id,
                        ["ip"] = cellEntity.MailBox.Ip,
                        ["port"] = cellEntity.MailBox.Port,
                        ["hostNum"] = cellEntity.MailBox.HostNum,
                    },
                    ["entityClassName"] = cellEntity.GetType().Name,
                    ["cellEntityId"] = string.Empty,
                });
            }

            Logger.Debug("Send all entities to web mgr");
            var res = MessageQueueJsonBody.Create(
                msgId,
                entities);
            Logger.Debug("Send all entities to web mgr sent");
            this.messageQueueClientToWebMgr!.Publish(
                res.ToJson(),
                Consts.ServerExchangeName,
                Consts.AllEntitiesRes);
        }
    }
}