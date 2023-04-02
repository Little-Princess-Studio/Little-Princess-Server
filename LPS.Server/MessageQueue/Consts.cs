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
    /// Routing key from web manager.
    /// </summary>
    public const string GetServerBasicInfoRoutingKey = "getServerBasicInfo.toHostMgr";

    /// <summary>
    /// Request to collection server info.
    /// </summary>
    public const string CollectServerInfo = "collectionServerInfo.toSrv";

    /// <summary>
    /// Server info res.
    /// </summary>
    public const string ServerInfo = "serverInfo.toWebMgr";

    /// <summary>
    /// Server count result routing key to web manager.
    /// </summary>
    public const string ServerBasicInfoResRoutingKey = "serverBasicInfoRes.toWebMgr";

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
}