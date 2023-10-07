// -----------------------------------------------------------------------
// <copyright file="ServiceManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Service.Instance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server;
using LPS.Server.Database;
using LPS.Server.Instance.ConnectionManager;
using LPS.Server.Instance.HostConnection;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using LPS.Server.Service;
using Newtonsoft.Json.Linq;
using static LPS.Server.Instance.ConnectionManager.ServiceManagerConnectionManager;

/// <summary>
/// Represents a service instance, which contains multiple LPS service instance.
/// </summary>
public class ServiceManager : IInstance
{
    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.ServiceManager;

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

    private readonly TcpServer tcpServer;
    private readonly Random random = new();
    private readonly int desiredServiceNum;
    private readonly Dictionary<string, ServiceRoutingMapDescriptor> serviceRoutingMap = new();
    private readonly MD5 md5 = MD5.Create();
    private readonly AsyncTaskGenerator<(ServiceRpcCallBack Callback, Connection Connection), Connection> serviceRpcCallbackAsyncTaskGenerator = new();
    private readonly AsyncTaskGenerator<(EntityRpcCallBack Callback, string Identifier), string> entityRpcCallBackAsyncTaskGenerator = new();
    private readonly Dictionary<Common.Rpc.MailBox, Connection> mailBoxToServiceConn = new();
    private readonly Dispatcher<(IMessage Mesage, string TargetIdentifier, InstanceType OriType)> dispatcher = new();
    private readonly Common.Rpc.MailBox mailBox;

    private IManagerConnection hostMgrConnection = null!;

    private ServiceManagerConnectionManager connectionManager = null!;

    private State state = State.Init;

    private int unreadyServiceNum;
    private uint hostMgrConnectionIdCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceManager"/> class.
    /// </summary>
    /// <param name="name">Name of the server.</param>
    /// <param name="ip">Ip of the server.</param>
    /// <param name="port">Port of the server.</param>
    /// <param name="hostNum">Hostnum of the server.</param>
    /// <param name="hostManagerIp">Ip of the hostmanager.</param>
    /// <param name="hostManagerPort">Port of the hostmanager.</param>
    /// <param name="useMqToHostMgr">If use message queue to build connection with host manager.</param>
    /// <param name="desiredServiceNum">The desired number of services to be registered.</param>
    /// <param name="config">Config of the instance.</param>
    public ServiceManager(
        string name,
        string ip,
        int port,
        int hostNum,
        string hostManagerIp,
        int hostManagerPort,
        bool useMqToHostMgr,
        int desiredServiceNum,
        JToken config)
    {
        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
        this.desiredServiceNum = desiredServiceNum;
        this.Config = config;

        this.tcpServer = new TcpServer(this.Ip, this.Port)
        {
            OnInit = this.RegisterServerMessageHandlers,
            OnDispose = this.UnregisterServerMessageHandlers,
        };

        this.mailBox = new(string.Empty, this.Ip, this.Port, this.HostNum);

        this.InitHostManagerConnection(hostManagerIp, hostManagerPort, useMqToHostMgr);
        this.InitConnectionManager();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        this.state = State.WaitForServiceInstanceRegister;
        this.tcpServer.Run();
        this.hostMgrConnection.Run();

        this.tcpServer.WaitForExit();
        this.hostMgrConnection.WaitForExit();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.tcpServer.Stop();
        this.hostMgrConnection.ShutDown();
    }

    private void RegisterServerMessageHandlers()
    {
        this.tcpServer.RegisterMessageHandler(PackageType.ServiceRpc, this.HandleServiceRpc);
        this.tcpServer.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleServiceRpcCallBack);
        this.tcpServer.RegisterMessageHandler(PackageType.ServiceControl, this.HandleServiceControl);
        this.tcpServer.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.tcpServer.RegisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleEntityRpcCallBack);
    }

    private void UnregisterServerMessageHandlers()
    {
        this.tcpServer.UnregisterMessageHandler(PackageType.ServiceRpc, this.HandleServiceRpc);
        this.tcpServer.UnregisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleServiceRpcCallBack);
        this.tcpServer.UnregisterMessageHandler(PackageType.ServiceControl, this.HandleServiceControl);
        this.tcpServer.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        this.tcpServer.UnregisterMessageHandler(PackageType.EntityRpcCallBack, this.HandleEntityRpcCallBack);
    }

    private void HandleEntityRpc((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("EntityRpc received.");
        var entityRpc = (EntityRpc)arg.Message;
        var senderId = entityRpc.SenderMailBox.ID;

        var (task, id) =
            this.entityRpcCallBackAsyncTaskGenerator.GenerateAsyncTask(
                entityRpc.ServiceInstanceId,
                5000,
                (rpcId) => new RpcTimeOutException($"Entity RPC timeout."));
        entityRpc.ServiceManagerRpcId = id;
        task.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Logger.Error(t.Exception, $"Entity RPC failed.");
                return;
            }

            var (callback, identifier) = t.Result;
            this.connectionManager.SendMessage(identifier, callback, ConnectionType.Service);
        });

        Logger.Debug($"Entity RPC sent to gate");
        this.connectionManager.SendMessageToRandomGate(entityRpc);
    }

    private void HandleEntityRpcCallBack((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("EntityRpcCallBack received.");
        var callback = (EntityRpcCallBack)arg.Message;
        var serviceMgrRpcId = callback.ServiceManagerRpcId;
        var identifier =
            this.entityRpcCallBackAsyncTaskGenerator.GetDataByAsyncTaskId(serviceMgrRpcId);
        this.entityRpcCallBackAsyncTaskGenerator.ResolveAsyncTask(serviceMgrRpcId, (callback, identifier));
    }

    private void HandleServiceRpc((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("ServiceRpc received.");
        var serviceRpc = (arg.Message as ServiceRpc)!;
        var senderConn = arg.Connection;

        // choose the shard to send the service rpc.
        var serviceName = serviceRpc.ServiceName;
        var desc = this.serviceRoutingMap[serviceName];
        var shardCnt = (uint?)desc?.ShardCount;

        if (shardCnt is not null)
        {
            uint shard;
            if (serviceRpc.RandomShard)
            {
                shard = (uint)this.random.Next((int)shardCnt);
            }
            else
            {
                var id = serviceRpc.SenderMailBox.ID;
                var encoded = this.md5.ComputeHash(Encoding.UTF8.GetBytes(id));
                shard = (uint)(BitConverter.ToUInt32(encoded, 0) % shardCnt);
            }

            serviceRpc.ShardID = (uint)shard;

            var serviceMb = desc!.GetShardMailBox(shard);
            var serviceConn = this.mailBoxToServiceConn[serviceMb];
            if (serviceConn is not null)
            {
                var (task, id) =
                    this.serviceRpcCallbackAsyncTaskGenerator.GenerateAsyncTask(
                        senderConn,
                        5000,
                        (rpcId) => new RpcTimeOutException($"Service RPC timeout: {serviceName}:{serviceRpc.MethodName}."));
                serviceRpc.ServiceManagerRpcId = id;
                task.ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception, $"Service RPC {serviceName}:{serviceRpc.MethodName} failed.");
                        return;
                    }

                    var (callback, conn) = t.Result;
                    var pkg = PackageHelper.FromProtoBuf(callback, 0).ToBytes();
                    conn.Socket.Send(pkg);
                });

                Logger.Debug($"Servce RPC {serviceName}:{serviceRpc.MethodName} sent to {serviceName}:{shard}");
                var pkg = PackageHelper.FromProtoBuf(serviceRpc, 0);
                serviceConn.Socket.Send(pkg.ToBytes());
            }
            else
            {
                var e = new Exception($"Service {serviceName}:{shard} is not exists.");
                Logger.Error(e);
            }
        }
        else
        {
            var e = new Exception($"Service {serviceName}is not exists.");
            Logger.Error(e);
        }
    }

    private void HandleServiceRpcCallBack((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info($"ServiceRpcCallBack received.");

        var callback = (arg.Message as ServiceRpcCallBack)!;
        var serviceMgrRpcId = callback.ServiceManagerRpcId;
        Logger.Debug($"service manager rpc id recieved: {callback.ServiceManagerRpcId}");
        var conn = this.serviceRpcCallbackAsyncTaskGenerator.GetDataByAsyncTaskId(callback.ServiceManagerRpcId);
        this.serviceRpcCallbackAsyncTaskGenerator.ResolveAsyncTask(serviceMgrRpcId, (callback, conn));
    }

    private void HandleServiceControl((IMessage Message, Connection Connection, uint RpcId) tuple)
    {
        var (msg, conn, _) = tuple;
        var ctl = (ServiceControl)msg;

        switch (ctl.Message)
        {
            case ServiceControlMessage.Ready:
                this.HandleServiceControlReady(ctl, conn);
                break;
            case ServiceControlMessage.ServiceReady:
                if (!this.CheckStateIn(State.WaitForServicesRegister))
                {
                    break;
                }

                this.RegisterServiceRoute(ctl);
                break;
            case ServiceControlMessage.Restarted:
                break;
            case ServiceControlMessage.ShutDown:
                break;
        }
    }

    private void HandleServiceControlReady(ServiceControl ctlMsg, Connection conn)
    {
        if (ctlMsg.From == ServiceRemoteType.Service)
        {
            if (!this.CheckStateIn(State.WaitForServiceInstanceRegister))
            {
                return;
            }

            _ = this.GenerateMailBoxForService(ctlMsg, conn)
                .ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Logger.Error(t.Exception, $"Failed to generate mailbox for service.");
                    return;
                }

                if (this.mailBoxToServiceConn.Count == this.desiredServiceNum)
                {
                    this.state = State.WaitForServicesRegister;
                    this.NotifyServiceInstancesToStartServices();
                }
            });
        }
        else if (ctlMsg.From == ServiceRemoteType.Gate)
        {
            Logger.Info($"Gate {ctlMsg.Args[0].Unpack<Common.Rpc.InnerMessages.MailBox>()} is ready.");
            var mb = ctlMsg.Args[0].Unpack<Common.Rpc.InnerMessages.MailBox>();
            this.connectionManager.RegisterImmediateConnection(conn, ConnectionType.Gate, mb.ID);
        }
        else if (ctlMsg.From == ServiceRemoteType.Server)
        {
            Logger.Info($"Server {ctlMsg.Args[0].Unpack<Common.Rpc.InnerMessages.MailBox>()} is ready.");
            var mb = ctlMsg.Args[0].Unpack<Common.Rpc.InnerMessages.MailBox>();
            this.connectionManager.RegisterImmediateConnection(conn, ConnectionType.Server, mb.ID);
        }
        else
        {
            var e = new Exception($"Invalid service remote type {ctlMsg.From}.");
            Logger.Error(e);
        }
    }

    private void InitHostManagerConnection(string hostManagerIp, int hostManagerPort, bool useMqToHostMgr)
    {
        if (!useMqToHostMgr)
        {
            this.hostMgrConnection = new ImmediateHostManagerConnectionOfServiceManager(
                hostManagerIp,
                hostManagerPort,
                () => this.tcpServer!.Stopped);
        }
        else
        {
            this.hostMgrConnection = new MessageQueueHostManagerConnectionOfServiceManager();
        }

        this.hostMgrConnection.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommand);
        this.hostMgrConnection.RegisterMessageHandler(PackageType.Ping, this.HandlePing);
    }

    private void HandlePing(IMessage message)
    {
        var pong = new Pong()
        {
            SenderMailBox = RpcHelper.RpcMailBoxToPbMailBox(this.mailBox),
        };

        this.hostMgrConnection.Send(pong);
    }

    private void HandleHostCommand(IMessage message)
    {
        throw new NotImplementedException();
    }

    private uint GenerateAsyncId()
    {
        return this.hostMgrConnectionIdCounter++;
    }

    private void RegisterServiceRoute(ServiceControl ctlMsg)
    {
        var mb = RpcHelper.PbMailBoxToRpcMailBox(ctlMsg.Args[0].Unpack<MailBoxArg>().PayLoad);
        var serviceName = RpcHelper.GetString(ctlMsg.Args[1]);
        var shard = (uint)RpcHelper.GetInt(ctlMsg.Args[2]);

        if (this.serviceRoutingMap.ContainsKey(serviceName))
        {
            var desc = this.serviceRoutingMap[serviceName];
            desc.RegisterShard(shard, mb);
            if (desc.AllShardReady)
            {
                --this.unreadyServiceNum;
                if (this.unreadyServiceNum <= 0)
                {
                    this.state = State.Open;
                    Logger.Info("All services are ready, open service manager");
                    var hostMsg = new Control()
                    {
                        Message = ControlMessage.Ready,
                        From = RemoteType.ServiceManager,
                    };

                    hostMsg.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.mailBox)));

                    this.hostMgrConnection.Send(hostMsg);

                    var readyMsg = new ServiceManagerCommand()
                    {
                        Type = ServiceManagerCommandType.AllServicesReady,
                    };

                    var pkg = PackageHelper.FromProtoBuf(readyMsg, 0);
                    foreach (var conn in this.mailBoxToServiceConn.Values)
                    {
                        conn.Socket.Send(pkg.ToBytes());
                    }
                }
            }
        }
    }

    private void NotifyServiceInstancesToStartServices()
    {
        Logger.Info("Notify service instance to create services");

        var assignResult = ServiceHelper.AssignServicesToServiceInstances(this.mailBoxToServiceConn.Count);

        var shardDict = new Dictionary<string, List<int>>();

        foreach (var assignedInfo in assignResult)
        {
            foreach (var (serviceName, shardList) in assignedInfo)
            {
                if (!shardDict.ContainsKey(serviceName))
                {
                    shardDict[serviceName] = new List<int>();
                }

                shardDict[serviceName].AddRange(shardList);
            }
        }

        foreach (var (serviceName, shardList) in shardDict)
        {
            var routingDesc = new ServiceRoutingMapDescriptor(shardList.Select(x => (uint)x));
            this.serviceRoutingMap[serviceName] = routingDesc;
        }

        this.unreadyServiceNum = assignResult.Count;
        Logger.Debug($"unreadyServiceNum: {this.unreadyServiceNum}");
        var idx = 0;
        foreach (var (mailbox, conn) in this.mailBoxToServiceConn)
        {
            var dict = assignResult[idx++];
            var cmd = new ServiceManagerCommand()
            {
                Type = ServiceManagerCommandType.CreateNewServices,
            };

            var serviceDict = new DictWithStringKeyArg();

            var generateTaskList = new List<Task>();
            foreach (var pair in dict)
            {
                var serviceName = pair.Key;
                var serviceShards = pair.Value;

                var serviceShardIdTasks =
                    serviceShards.Select(_ => DbHelper.GenerateNewGlobalId());

                // wait for generating ids.
                var generateTaskForService = Task.WhenAll(serviceShardIdTasks)
                    .ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception, $"Failed to generate ids for service {serviceName}.");
                        return;
                    }

                    var ids = t.Result;
                    var shardRpcDict = new DictWithIntKeyArg();
                    Logger.Debug($"Service {serviceName} ids: {string.Join(',', ids)}");

                    int i = 0;
                    foreach (var shard in serviceShards)
                    {
                        shardRpcDict.PayLoad.Add(shard, Any.Pack(new StringArg()
                        {
                            PayLoad = ids[i],
                        }));
                        ++i; // increment i
                    }

                    serviceDict.PayLoad.Add(serviceName, Any.Pack(message: shardRpcDict));
                });

                generateTaskList.Add(generateTaskForService);
            }

            Task.WhenAll(generateTaskList).Wait();

            cmd.Args.Add(Any.Pack(serviceDict));

            var pkg = PackageHelper.FromProtoBuf(cmd, 0);
            var bytes = pkg.ToBytes();
            conn.Socket.Send(bytes);
            Logger.Info($"Send command {cmd.Type} to service instance");
        }
    }

    private Task GenerateMailBoxForService(ServiceControl ctlMsg, Connection conn)
    {
        return DbHelper.GenerateNewGlobalId().ContinueWith(t =>
        {
            var id = t.Result;
            var ip = RpcHelper.GetString(ctlMsg.Args[0]);
            var port = RpcHelper.GetInt(ctlMsg.Args[1]);
            var hostNum = RpcHelper.GetInt(ctlMsg.Args[2]);

            var mailBox = new Common.Rpc.MailBox(id, ip, port, hostNum);
            Logger.Info($"Generate mailbox for service {mailBox}.");

            var cmd = new ServiceManagerCommand()
            {
                Type = ServiceManagerCommandType.Start,
            };

            cmd.Args.Add(Any.Pack(new MailBoxArg()
            {
                PayLoad = RpcHelper.RpcMailBoxToPbMailBox(mailBox),
            }));

            var pkg = PackageHelper.FromProtoBuf(cmd, 0);
            var bytes = pkg.ToBytes();

            conn.Socket.Send(bytes);

            lock (this.mailBoxToServiceConn)
            {
                this.mailBoxToServiceConn.Add(mailBox, conn);
                this.connectionManager.RegisterImmediateConnection(
                    conn, ConnectionType.Service, id);
            }
        });
    }

    private bool CheckStateIn(params State[] states)
    {
        if (!states.Contains(this.state))
        {
            var e = new Exception($"Service manager is not in state {string.Join(',', states.Select(s => s.ToString()))}, but {this.state}");
            Logger.Warn(e);
            return false;
        }

        return true;
    }

    private void InitConnectionManager()
    {
        var gateConnectionMap = new ConnectionMap(
            sendImmediateMessage: (conn, msg) =>
            {
                var pkg = PackageHelper.FromProtoBuf(msg, 0);
                conn.Socket.Send(pkg.ToBytes());
            },
            sendMessageQueueMessage: (conn, msg) =>
            {
                throw new NotImplementedException();
            });

        var serverConnectionMap = new ConnectionMap(
            sendImmediateMessage: (conn, msg) =>
            {
                var pkg = PackageHelper.FromProtoBuf(msg, 0);
                conn.Socket.Send(pkg.ToBytes());
            },
            sendMessageQueueMessage: (conn, msg) =>
            {
                throw new NotImplementedException();
            });

        var serviceConnectionMap = new ConnectionMap(
            sendImmediateMessage: (conn, msg) =>
            {
                var pkg = PackageHelper.FromProtoBuf(msg, 0);
                conn.Socket.Send(pkg.ToBytes());
            },
            sendMessageQueueMessage: (conn, msg) =>
            {
                throw new NotImplementedException();
            });

        this.connectionManager = new ServiceManagerConnectionManager(
            gateConnectionMap: gateConnectionMap,
            serviceConnectionMap: serviceConnectionMap,
            serverConnectionMap: serverConnectionMap);
    }

    private enum State
    {
        Init,
        WaitForServiceInstanceRegister,
        WaitForServicesRegister,
        Open,
    }

    private class ServiceRoutingMapDescriptor
    {
        public readonly int ShardCount;

        public bool AllShardReady => this.unreadyShards is null;

        private readonly Dictionary<uint, Common.Rpc.MailBox> shardToMbMap = new();

        private HashSet<uint>? unreadyShards;

        public ServiceRoutingMapDescriptor(IEnumerable<uint> shards)
        {
            this.unreadyShards = new HashSet<uint>(shards);
            this.ShardCount = shards.Count();
        }

        public Common.Rpc.MailBox GetShardMailBox(uint shard) => this.shardToMbMap[shard];

        public void RegisterShard(uint shard, Common.Rpc.MailBox mb)
        {
            if (this.unreadyShards != null && this.unreadyShards.Contains(shard))
            {
                this.unreadyShards.Remove(shard);
            }

            if (!this.shardToMbMap.ContainsKey(shard))
            {
                this.shardToMbMap[shard] = mb;
            }
            else
            {
                Logger.Warn($"Shard {shard} already registered.");
                return;
            }

            if (this.unreadyShards != null && this.unreadyShards.Count == 0)
            {
                this.unreadyShards = null;
            }
        }

        public string DebugString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shard count: {this.ShardCount}");
            if (this.unreadyShards is not null)
            {
                sb.AppendLine($"Unready shards: {string.Join(',', this.unreadyShards)}");
            }

            sb.AppendLine($"Shard to mailbox map: {string.Join(',', this.shardToMbMap.Select(x => $"{x.Key}:{x.Value}"))}");
            return sb.ToString();
        }
    }
}