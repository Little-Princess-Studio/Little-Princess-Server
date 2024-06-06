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
    /// The name of the exchange used for communication between the host manager and service manager.
    /// </summary>
    public const string HostMgrToServiceMgrExchangeName = "hostmgrToServiceMgr.exchange";

    /// <summary>
    /// Exchange name of server to host manager.
    /// </summary>
    public const string GateToHostExchangeName = "gateToHostMgr.exchange";

    /// <summary>
    /// The name of the exchange used for communication between the service manager and the host.
    /// </summary>
    public const string ServiceMgrToHostExchangeName = "serviceMgrToHost.exchange";

    /// <summary>
    /// The name of the exchange used for communication between the service manager and service.
    /// </summary>
    public const string ServiceMgrToServiceExchangeName = "serviceMgrToService.exchange";

    /// <summary>
    /// The name of the exchange used for communication between the service manager and service.
    /// </summary>
    public const string ServiceMgrToServerExchangeName = "serviceMgrToServer.exchange";

    /// <summary>
    /// The name of the exchange used for communication between the service manager and gate.
    /// </summary>
    public const string ServiceMgrToGateExchangeName = "serviceMgrToGate.exchange";

    /// <summary>
    /// Exchange name of database manager to server.
    /// </summary>
    public const string DbMgrToDbClientExchangeName = "dbmgrToDbClient.exchange";

    /// <summary>
    /// Exchange name of server to database manager.
    /// </summary>
    public const string DbClientToDbMgrExchangeName = "dbClientToDbmgr.exchange";

    /// <summary>
    /// Exchange name of service to service manager.
    /// </summary>
    public const string ServiceToServiceMgrExchangeName = "serviceToServiceMgr.exchange";

    /// <summary>
    /// Exchange name of server to service manager.
    /// </summary>
    public const string ServerToServiceMgrExchangeName = "serverToServiceMgr.exchange";

    /// <summary>
    /// Exchange name of gate to service manager.
    /// </summary>
    public const string GateToServiceMgrExchangeName = "gateToServiceMgr.exchange";

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
    /// Host message package routing key from service manager to server.
    /// </summary>
    /// <param name="targetName">Target name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateServiceManagerMessageToServerPackage(string targetName) => $"serviceManagerMessagePackage.{targetName}.serviceMgrToSrv";

    /// <summary>
    /// Host message package routing key from service manager to gate.
    /// </summary>
    /// <param name="targetName">Target name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateServiceManagerMessageToGatePackage(string targetName) => $"serviceManagerMessagePackage.{targetName}.serviceMgrToSrv";

    /// <summary>
    /// Host message package routing key from service manager to service.
    /// </summary>
    /// <param name="targetName">Target name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateServiceManagerMessageToServicePackage(string targetName) => $"serviceManagerMessagePackage.{targetName}.serviceMgrToSrv";

    /// <summary>
    /// Host message package routing key to service manager.
    /// </summary>
    public const string HostMessagePackageToServiceMgrPackage = "hostMessagePackage.toServiceMgr";

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
    /// The message to request server ping-pong information from the host manager.
    /// </summary>
    public const string GetServerPingPongInfo = "getServerPingPongInfo.toHostMgr";

    /// <summary>
    /// Request to get server detailed info.
    /// </summary>
    public const string GetServerDetailedInfo = "getServerDetailedInfo.webmgr.toSrv";

    /// <summary>
    /// Server detailed info res.
    /// </summary>
    public const string ServerDetailedInfo = "serverDetailedInfo.toWebMgr";

    /// <summary>
    /// Server count result routing key to web manager.
    /// </summary>
    public const string ServerBasicInfoRes = "serverBasicInfoRes.toWebMgr";

    /// <summary>
    /// The message queue response topic for getting server ping-pong information.
    /// </summary>
    public const string GetServerPingPongInfoRes = "getServerPingPongInfoRes.toWebMgr";

    /// <summary>
    /// Get all entities of server.
    /// </summary>
    public const string GetAllEntitiesOfServer = "getAllEntitiesOfServer.webmgr.toSrv";

    /// <summary>
    /// Result of all entities of server.
    /// </summary>
    public const string AllEntitiesRes = "allEntitiesRes.toWebMgr";

    /// <summary>
    /// Routing keys to host manager.
    /// </summary>
    public const string RoutingKeyToHostManager = "#.toHostMgr";

    /// <summary>
    /// Routing keys of web manager to server.
    /// </summary>
    public const string RoutingKeyWebManagerToServer = "#.webmgr.toSrv";

    /// <summary>
    /// Get routing key of server to observe.
    /// </summary>
    /// <param name="targetName">Server name.</param>
    /// <returns>Routing key of server to observe.</returns>
    public static string GetRoutingKeyToServer(string targetName) => $"*.{targetName}.toSrv";

    /// <summary>
    /// Get routing key of Gate to observe.
    /// </summary>
    /// <param name="targetName">Gate name.</param>
    /// <returns>Routing key of server to observe.</returns>
    public static string GetRoutingKeyToGate(string targetName) => $"*.{targetName}.toGate";

    /// <summary>
    /// Get routing key of Service to observe.
    /// </summary>
    /// <param name="targetName">Service name.</param>
    /// <returns>Routing key of service to observe.</returns>
    public static string GetRoutingKeyFromServiceManagerToService(string targetName) => $"*.{targetName}.toService";

    /// <summary>
    /// Get routing key of Server to observe.
    /// </summary>
    /// <param name="targetName">Server name.</param>
    /// <returns>Routing key of service to observe.</returns>
    public static string GetRoutingKeyFromServiceManagerToServer(string targetName) => $"*.{targetName}.serviceMgrToSrv";

    /// <summary>
    /// Get routing key of Gate to observe.
    /// </summary>
    /// <param name="targetName">Gate name.</param>
    /// <returns>Routing key of service to observe.</returns>
    public static string GetRoutingKeyFromServiceManagerToGate(string targetName) => $"*.{targetName}.serviceMgrToSrv";

    /// <summary>
    /// The routing key used to send messages to the service manager.
    /// </summary>
    public const string RoutingKeyToServiceMgr = "#.toServiceMgr";

    /// <summary>
    /// Routing key from database manager to client.
    /// </summary>
    public const string RoutingKeyDbMgrToClient = "#.dbMgrToClient";

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
    /// Message queue name for server message queue recieved from gate.
    /// </summary>
    public const string GateMessageQueueName = "gatemsg_que";

    /// <summary>
    /// Message queue name for server message queue recieved from server.
    /// </summary>
    public const string ServerMessageQueueName = "srvmsg_que";

    /// <summary>
    /// Message queue name for database manager message queue recieved from server.
    /// </summary>
    public const string DbClientToDbMgrMessageQueueName = "dbmgr_from_db_client_que";

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
    /// The name of the message queue used by the service manager.
    /// </summary>
    public const string ServiceManagerQueueName = "serviceMgr_que";

    /// <summary>
    /// The name of the message queue used by the service to service manager.
    /// </summary>
    public const string ServiceOfServiceManagerQueueName = "serviceOfServiceMgr_que";

    /// <summary>
    /// The name of the message queue used by the gate to service manager service.
    /// </summary>
    public const string GateOfServiceManagerQueueName = "gateOfServiceMgr_que";

    /// <summary>
    /// The name of the message queue used by the server to service manager.
    /// </summary>
    public const string ServerOfServiceManagerQueueName = "serverOfServiceMgr_que";

    /// <summary>
    /// The name of the message queue used to send message from service manager to host manager.
    /// </summary>
    public const string ServiceManagerMessageQueueName = "serviceMgrMsg_que";

    /// <summary>
    /// Generate the queue name observed by service.
    /// </summary>
    /// <param name="id">Unique id for the message queue.</param>
    /// <returns>Name.</returns>
    public static string GenerateServiceQueueName(string id) => $"service_Queue_{id}";

    /// <summary>
    /// Generate service message package routing key to service manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Name.</returns>
    public static string GenerateServiceToServiceMgrMessagePackage(string name) => $"serviceMessagePackage.{name}.serviceToServiceMgr";

    /// <summary>
    /// Generate server message package routing key to service manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Name.</returns>
    public static string GenerateServerToServiceMgrMessagePackage(string name) => $"serverMessagePackage.{name}.srvToServiceMgr";

    /// <summary>
    /// Generate gate message package routing key to service manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Name.</returns>
    public static string GenerateGateToServiceMgrMessagePackage(string name) => $"serverMessagePackage.{name}.gateToServiceMgr";

    /// <summary>
    /// Generate the queue name observed by database client process.
    /// </summary>
    /// <param name="id">Unique id for the message queue.</param>
    /// <returns>Name.</returns>
    public static string GenerateDbClientQueueName(string id) => $"dbclient_que_{id}";

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
    /// The message package name for messages sent from the service manager to the host manager.
    /// </summary>
    public const string ServiceMgrMessagePackage = "serverMessagePackage.serviceMgrToHost";

    /// <summary>
    /// Generates database client message package routing key to database manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateDbClientMessagePackageToDbMgr(string name) => $"dbClientMessagePackage.{name}.dbClientToDbMgr";

    /// <summary>
    /// Generates database client inner message package routing key to database manager.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateDbClientInnerMessagePackageToDbMgr(string name) => $"dbClientInnerMessagePackage.{name}.dbClientToDbMgr";

    /// <summary>
    /// Generates database manager message package routing key to database client.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateDbMgrMessagePackageToDbClient(string name) => $"dbMgrMessagePackage.{name}.dbMgrToDbClient";

    /// <summary>
    /// Generates database manager message inner package routing key to database client.
    /// </summary>
    /// <param name="name">Instance name.</param>
    /// <returns>Routing key.</returns>
    public static string GenerateDbMgrMessageInnerPackageToDbClient(string name) => $"dbMgrInnerMessagePackage.{name}.dbMgrToDbClient";

    /// <summary>
    /// Routing keys of server to host manaer.
    /// </summary>
    public const string RoutingKeyServerToHost = "#.#.srvToHost";

    /// <summary>
    /// Routing keys of server to host manaer.
    /// </summary>
    public const string RoutingKeyGateToHost = "#.#.gateToHost";

    /// <summary>
    /// The routing key used for messages sent from the service manager to the host.
    /// </summary>
    public const string RoutingKeyServiceMgrToHost = "#.serviceMgrToHost";

    /// <summary>
    /// Routing keys of server to database manager.
    /// </summary>
    public const string RoutingKeyDbClientToDbMgr = "#.#.dbClientToDbMgr";

    /// <summary>
    /// Routing keys of service to service manager.
    /// </summary>
    public const string RoutingKeyServiceToServiceMgr = "#.#.serviceToServiceMgr";

    /// <summary>
    /// Routing keys of gate to service manager.
    /// </summary>
    public const string RoutingKeyGateToServiceMgr = "#.#.gateToServiceMgr";

    /// <summary>
    /// Routing keys of server to service manager.
    /// </summary>
    public const string RoutingKeyServerToServiceMgr = "#.#.srvToServiceMgr";
}