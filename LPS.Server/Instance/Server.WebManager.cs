// -----------------------------------------------------------------------
// <copyright file="Server.WebManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System.Linq;
using LPS.Common.Debug;
using LPS.Common.Rpc.RpcProperty;
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

    /// <summary>
    /// Dump the live RpcProperty tree of one entity hosted by this server.
    /// Returning <c>null</c> when the entity is not local lets the other
    /// servers stay silent so the WebManager just gets the one true answer.
    /// </summary>
    [WebMgrHandler("getEntityPropertyDump.webmgr.toSrv")]
    private JToken? HandleGetEntityPropertyDump(JToken body)
    {
        var entityId = body["entityId"]!.ToString();

        // Try DistributeEntity first, then CellEntity. Either map's BaseEntity
        // implementation exposes GetPropertyTreeReadOnly().
        LPS.Common.Entity.BaseEntity? entity = null;
        string entityClassName;
        string cellEntityId;
        if (this.localEntityDict.TryGetValue(entityId, out var dist))
        {
            entity = dist;
            entityClassName = dist.GetType().Name;
            cellEntityId = dist.Cell.MailBox.Id;
        }
        else if (this.cells.TryGetValue(entityId, out var cell))
        {
            entity = cell;
            entityClassName = cell.GetType().Name;
            cellEntityId = string.Empty;
        }
        else
        {
            return null;
        }

        var tree = entity.GetPropertyTreeReadOnly();
        var props = new JArray();
        if (tree is not null)
        {
            foreach (var (name, prop) in tree)
            {
                props.Add(new JObject
                {
                    ["name"] = name,
                    ["setting"] = prop.Setting.ToString(),
                    ["containerType"] = prop.Value.GetType().Name,
                    ["value"] = this.SerializePropertyValue(prop),
                });
            }
        }

        return new JObject
        {
            ["entityId"] = entityId,
            ["entityClassName"] = entityClassName,
            ["mailbox"] = new JObject
            {
                ["id"] = entity.MailBox.Id,
                ["ip"] = entity.MailBox.Ip,
                ["port"] = entity.MailBox.Port,
                ["hostNum"] = entity.MailBox.HostNum,
            },
            ["cellEntityId"] = cellEntityId,
            ["isFrozen"] = entity.IsFrozen,
            ["isDestroyed"] = entity.IsDestroyed,
            ["properties"] = props,
        };
    }

    /// <summary>
    /// Best-effort JSON-friendly serialization of an RpcProperty. Plaint
    /// values (string/int/bool/MailBox) become their raw form; complex
    /// containers (RpcList / RpcDictionary) walk one level via the Children
    /// dict; anything else falls back to <c>ToString()</c>. Worst case is a
    /// human-readable string - we never throw out of the admin dump.
    /// </summary>
    private JToken SerializePropertyValue(RpcProperty prop)
    {
        try
        {
            var container = prop.Value;
            var raw = container.GetRawValue();

            // Plaint container: GetRawValue returns the underlying T directly.
            if (!ReferenceEquals(raw, container))
            {
                return JToken.FromObject(raw ?? (object)string.Empty);
            }

            // Complex container with Children (RpcDictionary etc.): one level deep.
            if (container.Children is { Count: > 0 } children)
            {
                var obj = new JObject();
                foreach (var (k, child) in children)
                {
                    var childRaw = child.GetRawValue();
                    obj[k] = ReferenceEquals(childRaw, child)
                        ? JToken.FromObject(child.ToString() ?? string.Empty)
                        : JToken.FromObject(childRaw ?? (object)string.Empty);
                }

                return obj;
            }

            return container.ToString() ?? string.Empty;
        }
        catch (System.Exception e)
        {
            return $"<serialize error: {e.Message}>";
        }
    }
}
