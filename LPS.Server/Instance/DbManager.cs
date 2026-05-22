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
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Database;
using LPS.Server.Database.Storage.MongoDb;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static LPS.Common.Rpc.InnerMessages.PackageHelper;

/// <summary>
/// Database Manager.
/// </summary>
public class DbManager : IInstance
{
    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Ip { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public int HostNum { get; }

    /// <inheritdoc/>
    public JToken Config { get; }

    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.DbManager;

    private readonly TcpClient clientToHostManager;

    // We only use mq to handle db request from other instances.
    private readonly MessageQueueClient messageQueueClientToOtherInstance;

    private readonly EnumDispatcher<HostCommandType, HostCommand> hostCommandDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbManager"/> class.
    /// </summary>
    /// <param name="name">Instance name (used as MailBox.Id and supervisor key).</param>
    /// <param name="ip">Ip.</param>
    /// <param name="port">Port.</param>
    /// <param name="hostNum">Hostnum.</param>
    /// <param name="hostManagerIp">Ip of the host manager.</param>
    /// <param name="hostManagerPort">Port of the host manager.</param>
    /// <param name="cacheInfo">Global cache info.</param>
    /// <param name="databaseInfo">Database info.</param>
    /// <param name="databaseApiProviderNamespace">Namespace of DatabaseApiProvider.</param>
    /// <param name="config">Config of the instance.</param>
    public DbManager(
        string name,
        string ip,
        int port,
        int hostNum,
        string hostManagerIp,
        int hostManagerPort,
        DbHelper.DbInfo cacheInfo,
        DbHelper.DbInfo databaseInfo,
        string databaseApiProviderNamespace,
        JToken config)
    {
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;

        // Earlier revisions hardcoded Name = "hostmgr" because DbManager never
        // talked to HostManager beyond opening a TCP socket. Now we register
        // properly via Control.Ready, so a real per-instance name is required
        // (it becomes the routing key suffix and the MailBox.Id).
        this.Name = name;
        this.Config = config;

        // Sub-dispatcher for HostCommand handlers (e.g. ShutdownInstance) -
        // same pattern Gate and Server use on their hostMgrConnection.
        this.hostCommandDispatcher = new EnumDispatcher<HostCommandType, HostCommand>(
            $"dbmanager.{name}.hostCommand", warnOnMissing: false);
        this.hostCommandDispatcher.ScanAndRegister<HostCommandHandlerAttribute>(this);

        this.messageQueueClientToOtherInstance = new MessageQueueClient();
        this.clientToHostManager = new TcpClient(
            hostManagerIp,
            hostManagerPort,
            new ConcurrentQueue<(TcpClient, IMessage, bool)>())
        {
            // Notify HostManager once the TCP socket is up: send the same
            // Control.Ready handshake every other instance kind uses. This
            // adds DbManager to instanceStatusManager so it appears in the
            // cluster-overview API + WebManager UI alongside Gates/Servers.
            OnConnected = client =>
            {
                var mailBox = new Common.Rpc.MailBox(this.Name, this.Ip, this.Port, this.HostNum);
                var regCtl = new Control
                {
                    From = RemoteType.Dbmanager,
                    Message = ControlMessage.Ready,
                };
                regCtl.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(mailBox)));
                client.Send(regCtl, false);
                Logger.Info($"[dbmanager:{this.Name}] Sent Control.Ready to HostManager.");
            },
        };

        // Receive PackageType.HostCommand on the TcpClient so HostManager
        // can target this DbManager for ShutdownInstance (and future
        // commands). Mirrors Gate/Server's hostMgrConnection.
        this.clientToHostManager.RegisterMessageHandler(
            PackageType.HostCommand,
            arg => this.HandleHostCommandFromHost(arg.Message));

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

            var extraAssemblies = new System.Reflection.Assembly[] { typeof(DbManager).Assembly };
            DbManagerHelper.ScanDbApis(databaseApiProviderNamespace, extraAssemblies);
            DbManagerHelper.ScanInnerDbApis("LPS.Server.Database.Storage.MongoDb", extraAssemblies);
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

        // Pump the TcpClient's inbound queue on a background thread so
        // HostCommand messages from HostManager (notably ShutdownInstance)
        // actually reach the registered handlers. TcpClient itself only
        // enqueues - dispatch requires an external Pump caller. Gate has
        // an equivalent clientsPumpMsgSandBox; for DbManager a single
        // upstream connection is enough so we inline the loop here.
        var pumpThread = new System.Threading.Thread(() =>
        {
            try
            {
                while (true)
                {
                    this.clientToHostManager.Pump();
                    System.Threading.Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[dbmanager:{this.Name}] pump thread stopped: {ex.Message}");
            }
        })
        {
            IsBackground = true,
            Name = $"dbmanager-{this.Name}-pump",
        };
        pumpThread.Start();

        this.clientToHostManager.WaitForExit();
        this.messageQueueClientToOtherInstance.ShutDown();
        Logger.Debug("DbManager Exit.");
    }

    private void HandleHostCommandFromHost(IMessage msg)
    {
        var hostCmd = (HostCommand)msg;
        Logger.Info($"[dbmanager:{this.Name}] Handle host command, cmd type: {hostCmd.Type}");
        this.hostCommandDispatcher.Dispatch(hostCmd.Type, hostCmd);
    }

    [HostCommandHandler(HostCommandType.ShutdownInstance)]
    private void OnShutdownInstance(HostCommand cmd)
    {
        ProcessExitCoordinator.Schedule(
            $"dbmanager:{this.Name}",
            this.DrainForShutdown,
            cmd.ShutdownTimeoutMs);
    }

    [HostCommandHandler(HostCommandType.Stop)]
    private void OnStop(HostCommand cmd)
    {
        _ = cmd;
        this.Stop();
    }

    /// <summary>
    /// Per-instance teardown invoked by <see cref="ProcessExitCoordinator"/>
    /// when WebManager requests a graceful shutdown. Closes the upstream MQ
    /// link (Db API consumer) and the HostManager TCP socket, then lets
    /// ProcessExitCoordinator call <c>Environment.Exit(0)</c> so
    /// StartupManager treats it as intentional and does not respawn.
    /// </summary>
    private void DrainForShutdown()
    {
        Logger.Info($"[dbmanager:{this.Name}] DrainForShutdown begin.");

        try
        {
            // Shut down the MQ consumer first so no new DB requests land
            // while we are tearing the storage layer down. In-flight async
            // HandleDbApiPackage / HandleDbInnerApiPackage tasks are fire-
            // and-forget today; if stronger drain semantics are needed they
            // can be added behind a SemaphoreSlim in a follow-up.
            this.messageQueueClientToOtherInstance?.ShutDown();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[dbmanager:{this.Name}] MQ ShutDown threw: {ex.Message}");
        }

        try
        {
            this.clientToHostManager?.Stop();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[dbmanager:{this.Name}] clientToHostManager Stop threw: {ex.Message}");
        }

        Logger.Info($"[dbmanager:{this.Name}] DrainForShutdown complete.");
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
            var resMsg = MessageParserWrapper<DatabaseManagerRpc>.Get();
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
            var resMsg = MessageParserWrapper<DatabaseManagerInnerRpc>.Get();
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