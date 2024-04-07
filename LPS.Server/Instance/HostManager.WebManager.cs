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
/// HostManager will watch the status of each component in the host including:
/// Server/Gate/DbManager
/// HostManager use ping/pong strategy to check the status of the components
/// if HostManager find any component looks like dead, it will
/// kick this component off from the host, try to create a new component
/// while writing alert log.
/// </summary>
public partial class HostManager : IInstance
{
    private void InitMessageQueueClientToWebManager()
    {
        Logger.Debug("Start mq client for web manager.");
        this.messageQueueClientToWebMgr.Init();
        this.messageQueueClientToWebMgr.AsProducer();
        this.messageQueueClientToWebMgr.AsConsumer();

        this.messageQueueClientToWebMgr.DeclareExchange(Consts.WebMgrExchangeName);
        this.messageQueueClientToWebMgr.DeclareExchange(Consts.ServerExchangeName);
        this.messageQueueClientToWebMgr.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.Name),
            Consts.WebMgrExchangeName,
            Consts.RoutingKeyToHostManager);

        this.messageQueueClientToWebMgr.Observe(
            Consts.GenerateWebManagerQueueName(this.Name),
            (msg, routingKey) =>
            {
                if (routingKey == Consts.GetServerBasicInfo)
                {
                    var (msgId, _) = MessageQueueJsonBody.From(msg);
                    var res = MessageQueueJsonBody.Create(
                        msgId,
                        new JObject
                        {
                            ["serverCnt"] = this.DesiredServerNum,
                            ["serverMailBoxes"] = new JArray(this.serversMailBoxes.Select(conn => new JObject
                            {
                                ["id"] = conn.Id,
                                ["ip"] = conn.Ip,
                                ["port"] = conn.Port,
                                ["hostNum"] = conn.HostNum,
                            })),
                        });
                    this.messageQueueClientToWebMgr.Publish(
                        res.ToJson(),
                        Consts.ServerExchangeName,
                        Consts.ServerBasicInfoRes);
                }
                else if (routingKey == Consts.GetServerPingPongInfo)
                {
                    var (msgId, _) = MessageQueueJsonBody.From(msg);

                    var res = new JObject
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

                    var body = MessageQueueJsonBody.Create(msgId, res);
                    this.messageQueueClientToWebMgr.Publish(
                        body.ToJson(),
                        Consts.ServerExchangeName,
                        Consts.GetServerPingPongInfoRes);
                }
            });
    }
}