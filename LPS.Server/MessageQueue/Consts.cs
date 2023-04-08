// -----------------------------------------------------------------------
// <copyright file="Consts.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

/// <summary>
/// Consts class.
/// </summary>
public static class Consts
{
    /// <summary>
    /// Exchange name of web manager to this server.
    /// </summary>
    public const string WebMgrExchangeName = "webmgr.exchange";

    /// <summary>
    /// Exchange name of this server to web manager.
    /// </summary>
    public const string ServerExchangeName = "server.exchange";

    /// <summary>
    /// Exchange name of host manager to server/gate.
    /// </summary>
    public const string HostMgrToServerExchangeName = "hostmgrToSrv.exchange";

    /// <summary>
    /// Exchange name of server to host manager.
    /// </summary>
    public const string ServerToHostExchangeName = "srvToHostMgr.exchange";

    /// <summary>
    /// Host message package routing key to server.
    /// </summary>
    public const string HostMessagePackage = "hostMessagePackage.toSrv";

    /// <summary>
    /// Routing key from web manager.
    /// </summary>
    public const string GetServerBasicInfo = "getServerBasicInfo.toHostMgr";

    /// <summary>
    /// Request to get server detailed info.
    /// </summary>
    public const string GetServerDetailedInfo = "getServerDetailedInfo.toSrv";

    /// <summary>
    /// Server detailed info res.
    /// </summary>
    public const string ServerDetailedInfo = "serverDetailedInfo.toWebMgr";

    /// <summary>
    /// Server count result routing key to web manager.
    /// </summary>
    public const string ServerBasicInfoRes = "serverBasicInfoRes.toWebMgr";

    /// <summary>
    /// Get all entities of server.
    /// </summary>
    public const string GetAllEntitiesOfServer = "getAllEntitiesOfServer.toSrv";

    /// <summary>
    /// Result of all entities of server.
    /// </summary>
    public const string AllEntitiesRes = "allEntitiesRes.toWebMgr";

    /// <summary>
    /// Routing keys to host manager.
    /// </summary>
    public const string RoutingKeyToHostManager = "#.toHostMgr";

    /// <summary>
    /// Routing keys to server.
    /// </summary>
    public const string RoutingKeyToServer = "#.toSrv";

    /// <summary>
    /// Routing keys to web manager.
    /// </summary>
    public const string RoutingKeyToWebManager = "#.toWebMgr";

    /// <summary>
    /// Name of the queue observed by web manager.
    /// </summary>
    public const string WebManagerQueueName = "webmgr_que";

    /// <summary>
    /// Generate the queue name observed by server process.
    /// </summary>
    /// <param name="id">Unique id for the message queue.</param>
    /// <returns>Name.</returns>
    public static string GenerateWebManagerQueueName(string id) => $"webmgr_que_{id}";

    /// <summary>
    /// Generate the queue name observed by server process.
    /// </summary>
    /// <param name="id">Unique id for the message queue.</param>
    /// <returns>Name.</returns>
    public static string GenerateHostManagerQueueName(string id) => $"hostmgr_que_{id}";
}