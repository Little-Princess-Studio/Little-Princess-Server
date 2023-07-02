// -----------------------------------------------------------------------
// <copyright file="DbManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Server.Database;
using LPS.Server.Database.Storage.MongoDb;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Database Manager.
/// </summary>
public class DbManager : IInstance
{
    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.DbManager;

    private readonly TcpClient clientToHostManager;

    // We only use mq to handle db request from other instances.
    private readonly MessageQueueClient messageQueueClientToHostMgr;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbManager"/> class.
    /// </summary>
    /// <param name="ip">Ip.</param>
    /// <param name="port">Port.</param>
    /// <param name="hostNum">Hostnum.</param>
    /// <param name="hostManagerIp">Ip of the host manager.</param>
    /// <param name="hostManagerPort">Port of the host manager.</param>
    /// <param name="cacheInfo">Global cache info.</param>
    /// <param name="databaseInfo">Database info.</param>
    /// <param name="databaseApiProviderNamespace">Namespace of DatabaseApiProvider.</param>
    public DbManager(
        string ip,
        int port,
        int hostNum,
        string hostManagerIp,
        int hostManagerPort,
        DbHelper.DbInfo cacheInfo,
        DbHelper.DbInfo databaseInfo,
        string databaseApiProviderNamespace)
    {
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;

        this.messageQueueClientToHostMgr = new MessageQueueClient();
        this.clientToHostManager = new TcpClient(
            hostManagerIp,
            hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>());

        // TODO: init mongodb by type full name.
        if (databaseInfo.DbType == "mongodb")
        {
            string connString;
            if (string.IsNullOrEmpty(databaseInfo.DbConfig.UserName))
            {
                connString = $"mongodb://{databaseInfo.DbConfig.UserName}:{databaseInfo.DbConfig.Password}@{databaseInfo.DbConfig.Ip}:{databaseInfo.DbConfig.Port}/{databaseInfo.DbConfig.DefaultDb}";
            }
            else
            {
                connString = $"mongodb://{databaseInfo.DbConfig.Ip}:{databaseInfo.DbConfig.Port}/{databaseInfo.DbConfig.DefaultDb}";
            }

            Logger.Info("[DbManager] Init mongodb with connection string: {0}", connString);
            DbManagerHelper.SetDatabase(new MongoDb(), connString);
            DbManagerHelper.ScanDbApis(databaseApiProviderNamespace);
        }
        else
        {
            throw new Exception($"Unsupported database type, {databaseInfo.DbType}");
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.clientToHostManager.Stop();
        this.messageQueueClientToHostMgr.ShutDown();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Debug($"Start dbmanager at {this.Ip}:{this.Port}");

        DbManagerHelper.Init();

        this.InitMqClient();
        this.clientToHostManager.Run();

        this.clientToHostManager.WaitForExit();
        this.messageQueueClientToHostMgr.ShutDown();
        Logger.Debug("DbManager Exit.");
    }

    private void InitMqClient()
    {
        Logger.Debug("Start mq client for host manager.");
        this.messageQueueClientToHostMgr.Init();
        this.messageQueueClientToHostMgr.AsProducer();
        this.messageQueueClientToHostMgr.AsConsumer();

        this.messageQueueClientToHostMgr.DeclareExchange(Consts.DbMgrToDbClientExchangeName);
        this.messageQueueClientToHostMgr.DeclareExchange(Consts.DbClientToDbMgrExchangeName);

        this.messageQueueClientToHostMgr.BindQueueAndExchange(
            Consts.DbClientToDbMgrMessageQueueName,
            Consts.DbClientToDbMgrExchangeName,
            Consts.RoutingKeyDbClientToDbMgr);

        this.messageQueueClientToHostMgr.Observe(
            Consts.DbClientToDbMgrMessageQueueName,
            this.HandleServerMqMessage);
    }

    private void HandleServerMqMessage(ReadOnlyMemory<byte> msg, string routingKey)
    {
        var split = routingKey.Split('.');
        var msgType = split[0];
        var targetIdentifier = split[1];
        Logger.Debug($"Message recieved from server. {msgType} {targetIdentifier} {routingKey}");

        switch (msgType)
        {
            case "dbClientMessagePackage":
                _ = this.HandleMsgPackage(msg, targetIdentifier);
                break;
            default:
                Logger.Warn($"Unknown message type: {msgType}");
                break;
        }
    }

    private async Task HandleMsgPackage(ReadOnlyMemory<byte> msg, string targetIdentifier)
    {
        using var stream = new MemoryStream(msg.ToArray());
        using var reader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(reader);
        JObject query = JObject.Load(jsonReader);

        uint? id;
        string? apiName;
        JArray? args;

        if ((id = query["id"]?.ToObject<uint>()) == null
            || (apiName = query["apiName"]?.ToObject<string>()) == null
            || (args = query["args"]?.ToObject<JArray>()) == null)
        {
            Logger.Warn("Invalid query.");
            return;
        }

        Logger.Info($"Call Db Api {apiName}");
        var res = await DbManagerHelper.CallDbApi(apiName, args);
        var bytes = Encoding.UTF8.GetBytes(res != null ? res.ToJson() : string.Empty);

        this.messageQueueClientToHostMgr.Publish(
            bytes,
            Consts.DbMgrToDbClientExchangeName,
            Consts.GenerateDbMgrMessagePackageToDbClient(targetIdentifier));
    }
}