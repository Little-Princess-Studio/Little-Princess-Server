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
/// Server-side endpoint queryable by the WebManager. Wiring is declarative
/// via <see cref="WebMgrHandlerAttribute"/>; the shared
/// <see cref="WebMgrDispatcher"/> handles MQ bind / reply lookup / logging.
/// </summary>
public partial class Server
{
    private void InitWebManagerMessageQueueClient()
    {
        Logger.Debug("Start mq client for web manager (server).");
        this.webMgrDispatcher = new WebMgrDispatcher(this.Name, this.Name);
        this.webMgrDispatcher.ScanAndRegister(this);
        this.webMgrDispatcher.Init(Consts.RoutingKeyWebManagerToServer);
    }

    /// <summary>
    /// Returns this server's identity + entity/cell counters but only when
    /// the requested mailbox matches this instance.
    /// </summary>
    [WebMgrHandler("getServerDetailedInfo.webmgr.toSrv")]
    private JToken? HandleGetServerDetailedInfo(JToken body)
    {
        var serverId = body["serverId"]!.ToString();
        var hostNum = body["hostNum"]!.ToObject<int>();

        if (serverId != this.entity!.MailBox.Id || hostNum != this.entity!.MailBox.HostNum)
        {
            return null;
        }

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
            ["entitiesCnt"] = this.localEntityDict.Count,
            ["cellCnt"] = this.cells.Count,
        };
    }

    /// <summary>Returns every distribute-entity and cell living on this server.</summary>
    [WebMgrHandler("getAllEntitiesOfServer.webmgr.toSrv")]
    private JToken? HandleGetAllEntitiesOfServer(JToken body)
    {
        var serverId = body["serverId"]!.ToString();
        var hostNum = body["hostNum"]!.ToObject<int>();

        if (serverId != this.entity!.MailBox.Id || hostNum != this.entity!.MailBox.HostNum)
        {
            return null;
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
                ["mailbox"] = new JObject
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

        return entities;
    }
}
