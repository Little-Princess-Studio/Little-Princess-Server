// -----------------------------------------------------------------------
// <copyright file="DbManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Server.Database;
using LPS.Server.Database.Storage.MongoDb;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
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
    private readonly MessageQueueClient messageQueueClientToOtherInstance;

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

        this.messageQueueClientToOtherInstance = new MessageQueueClient();
        this.clientToHostManager = new TcpClient(
            hostManagerIp,
            hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>());

        // TODO: init mongodb by type full name.
        if (databaseInfo.DbType == "mongodb")
        {
            string connString;
            if (!string.IsNullOrEmpty(databaseInfo.DbConfig.UserName))
            {
                connString = $"mongodb://{databaseInfo.DbConfig.UserName}:{databaseInfo.DbConfig.Password}@{databaseInfo.DbConfig.Ip}:{databaseInfo.DbConfig.Port}/{databaseInfo.DbConfig.DefaultDb}";
            }
            else
            {
                connString = $"mongodb://{databaseInfo.DbConfig.Ip}:{databaseInfo.DbConfig.Port}/{databaseInfo.DbConfig.DefaultDb}";
            }

            Logger.Info("[DbManager] Init mongodb with connection string: ", connString);
            DbManagerHelper.SetDatabase(new MongoDbWrapper(databaseInfo.DbConfig.DefaultDb), connString);
            DbManagerHelper.ScanDbApis(databaseApiProviderNamespace);
            DbManagerHelper.ScanInnerDbApis("LPS.Server.Database.Storage.MongoDb");
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
        this.messageQueueClientToOtherInstance.ShutDown();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Debug($"Start dbmanager at {this.Ip}:{this.Port}");

        DbManagerHelper.Init();

        this.InitMqClient();
        this.clientToHostManager.Run();

        this.clientToHostManager.WaitForExit();
        this.messageQueueClientToOtherInstance.ShutDown();
        Logger.Debug("DbManager Exit.");
    }

    private void InitMqClient()
    {
        Logger.Debug("Start mq client for host manager.");
        this.messageQueueClientToOtherInstance.Init();
        this.messageQueueClientToOtherInstance.AsProducer();
        this.messageQueueClientToOtherInstance.AsConsumer();

        this.messageQueueClientToOtherInstance.DeclareExchange(Consts.DbMgrToDbClientExchangeName);
        this.messageQueueClientToOtherInstance.DeclareExchange(Consts.DbClientToDbMgrExchangeName);

        this.messageQueueClientToOtherInstance.BindQueueAndExchange(
            Consts.DbClientToDbMgrMessageQueueName,
            Consts.DbClientToDbMgrExchangeName,
            Consts.RoutingKeyDbClientToDbMgr);

        this.messageQueueClientToOtherInstance.Observe(
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
                _ = this.HandleDbApiPackage(msg, targetIdentifier);
                break;
            case "dbClientInnerMessagePackage":
                _ = this.HandleDbInnerApiPackage(msg, targetIdentifier);
                break;
            default:
                Logger.Warn($"Unknown message type: {msgType}");
                break;
        }
    }

    private async Task HandleDbApiPackage(ReadOnlyMemory<byte> msg, string targetIdentifier)
    {
        try
        {
            var resMsg = new MessageParser<DatabaseManagerRpc>(() => new DatabaseManagerRpc());
            var databaseRpc = resMsg.ParseFrom(msg.ToArray());

            var id = databaseRpc.RpcId;
            var name = databaseRpc.ApiName;
            var args = databaseRpc.Args.ToArray();

            var res = await DbManagerHelper.CallDbApi(name, args);

            DatabaseManagerRpcRes? rpcRes = new()
            {
                RpcId = id,
                Res = Any.Pack(RpcHelper.RpcArgToProtoBuf(res)),
            };

            this.messageQueueClientToOtherInstance.Publish(
                rpcRes.ToByteArray(),
                Consts.DbMgrToDbClientExchangeName,
                Consts.GenerateDbMgrMessagePackageToDbClient(targetIdentifier));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HandleMsgPackage error.");
        }
    }

    private async Task HandleDbInnerApiPackage(ReadOnlyMemory<byte> msg, string targetIdentifier)
    {
        try
        {
            var resMsg = new MessageParser<DatabaseManagerInnerRpc>(() => new DatabaseManagerInnerRpc());
            var databaseRpc = resMsg.ParseFrom(msg.ToArray());

            var id = databaseRpc.RpcId;
            var name = databaseRpc.InnerApiName;
            var args = databaseRpc.Args.ToArray();

            var res = await DbManagerHelper.CallInnerDbApi(name, args);

            var rpcRes = new DatabaseManagerInnerRpcRes
            {
                RpcId = id,
                Res = res,
            };

            this.messageQueueClientToOtherInstance.Publish(
                rpcRes.ToByteArray(),
                Consts.DbMgrToDbClientExchangeName,
                Consts.GenerateDbMgrMessageInnerPackageToDbClient(targetIdentifier));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HandleMsgPackage error.");
        }
    }
}