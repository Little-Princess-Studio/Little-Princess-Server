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
    /// Exchange name of host manager to server/gate.
    /// </summary>
    public const string HostMgrToGateExchangeName = "hostmgrToGate.exchange";

    /// <summary>
    /// Exchange name of server to host manager.
    /// </summary>
    public const string GateToHostExchangeName = "gateToHostMgr.exchange";

    /// <summary>
    /// Host message package routing key to server.
    /// </summary>
    /// <param name="targetName">Target name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateHostMessageToServerPackage(string targetName) => $"hostMessagePackage.{targetName}.toSrv";

    /// <summary>
    /// Host message package routing key to gate.
    /// </summary>
    /// <param name="targetName">Target name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateHostMessageToGatePackage(string targetName) => $"hostMessagePackage.{targetName}.toGate";

    /// <summary>
    /// Host manager broadcast message package routing key to server.
    /// </summary>
    public const string HostBroadCastMessagePackageToServer = "hostBroadCastMessagePackage.toSrv";

    /// <summary>
    /// Host manager broadcast message package routing key to gate.
    /// </summary>
    public const string HostBroadCastMessagePackageToGate = "hostBroadCastMessagePackage.toGate";

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
    /// Routing keys to server.
    /// </summary>
    public const string RoutingKeyToGate = "#.toGate";

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
    public const string GateMessageQueueName = "gatemsg_que";

    /// <summary>
    /// Message queue name for server message queue.
    /// </summary>
    public const string ServerMessageQueueName = "srvmsg_que";

    /// <summary>
    /// Generate the queue name observed by gate process.
    /// </summary>
    /// <param name="id">Unique id for the message queue.</param>
    /// <returns>Name.</returns>
    public static string GenerateGateQueueName(string id) => $"gate_que_{id}";

    /// <summary>
    /// Generate the queue name observed by server process.
    /// </summary>
    /// <param name="id">Unique id for the message queue.</param>
    /// <returns>Name.</returns>
    public static string GenerateServerQueueName(string id) => $"srv_que_{id}";

    /// <summary>
    /// Generate server message package routing key to host manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateServerMessagePackage(string name) => $"serverMessagePackage.{name}.srvToHost";

    /// <summary>
    /// Generate gate message package routing key to host manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateGateMessagePackage(string name) => $"serverMessagePackage.{name}.gateToHost";

    /// <summary>
    /// Routing keys of server to host manaer.
    /// </summary>
    public const string RoutingKeyServerToHost = "#.#.srvToHost";

    /// <summary>
    /// Routing keys of server to host manaer.
    /// </summary>
    public const string RoutingKeyGateToHost = "#.#.gateToHost";
}