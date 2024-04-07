// -----------------------------------------------------------------------
// <copyright file="Gate.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Entity;
using LPS.Server.Instance.HostConnection;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using Newtonsoft.Json.Linq;
using MailBox = LPS.Common.Rpc.InnerMessages.MailBox;

/// <summary>
/// Each gate need maintain multiple connections from remote clients
/// and maintain a connection to hostmanager.
/// For hostmanager, gate is a client
/// for remote clients, gate is a server.
/// All the gate mailbox info will be saved in redis, and gate will
/// repeatly sync these info from redis.
/// </summary>
public partial class Gate : IInstance
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
    public InstanceType InstanceType => InstanceType.Gate;

    private readonly
        ConcurrentDictionary<(int, PackageType), Action<(IMessage Message, Connection Connection, uint RpcId)>>
        tcpClientsActions = new();

    private readonly ConcurrentQueue<(TcpClient Client, IMessage Message, bool IsReentry)> sendQueue = new();

    // private readonly SandBox clientsSendQueueSandBox_;
    private readonly ConcurrentDictionary<uint, Connection> createEntityMapping = new();

    private readonly Dictionary<string, (Common.Rpc.MailBox MailBox, Connection Connection)>
        entityIdToClientConnMapping = new();

    private readonly Random random = new();

    private readonly CountdownEvent serversMailBoxesReadyEvent = new CountdownEvent(1);
    private readonly CountdownEvent otherGatesMailBoxesReadyEvent = new CountdownEvent(1);
    private readonly CountdownEvent waitForSyncServiceManagerEvent;

    private readonly CountdownEvent localEntityGeneratedEvent;

    private readonly SandBox clientsPumpMsgSandBox;

    private readonly TcpServer tcpGateServer;

    private IManagerConnection hostMgrConnection = null!;
    private IManagerConnection serviceMgrConnection = null!;

    private GateEntity? entity;

    // if all the tcp clients have connected to server/other gate, countdownEvent_ will down to 0
    private CountdownEvent? allServersConnectedEvent;
    private CountdownEvent? allOtherGatesConnectedEvent;

    private CountdownEvent? gateClientsExitEvent;
    private CountdownEvent? serverClientsExitEvent;

    private Common.Rpc.MailBox serviceManagerMailBox;

    private uint createEntityCounter;

    private List<TcpClient>? tcpClientsToServer;
    private List<TcpClient>? tcpClientsToOtherGate;

    private bool readyToPumpClients;

    /// <summary>
    /// Initializes a new instance of the <see cref="Gate"/> class.
    /// </summary>
    /// <param name="name">Name of the gate.</param>
    /// <param name="ip">Ip of the gate.</param>
    /// <param name="port">Port of the gate.</param>
    /// <param name="hostNum">Hostnum of the gate.</param>
    /// <param name="hostManagerIp">Ip of the hostmanager.</param>
    /// <param name="hostManagerPort">Port of the hostmanager.</param>
    /// <param name="servers">All the servers info.</param>
    /// <param name="otherGates">All the other servers info.</param>
    /// <param name="useMqToHostMgr">If use message queue to build connection with host manager.</param>
    /// <param name="config">Config of the instance.</param>
    /// <param name="isRestart">If this instance restarting.</param>
    public Gate(
        string name,
        string ip,
        int port,
        int hostNum,
        string hostManagerIp,
        int hostManagerPort,
        (string IP, int Port)[] servers,
        (string InnerIp, string Ip, int Port)[] otherGates,
        bool useMqToHostMgr,
        JToken config,
        bool isRestart)
    {
        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
        this.Config = config;

        // tcp gate server handles msg from server/other gates
        this.tcpGateServer = new TcpServer(ip, port)
        {
            OnInit = this.RegisterMessageFromServerAndOtherGateHandlers,
            OnDispose = this.UnregisterMessageFromServerAndOtherGateHandlers,
        };

        this.localEntityGeneratedEvent = new CountdownEvent(1);
        this.waitForSyncServiceManagerEvent = new CountdownEvent(1);

        this.InitHostManagerConnection(useMqToHostMgr, hostManagerIp, hostManagerPort);

        this.clientsPumpMsgSandBox = SandBox.Create(this.PumpMessageHandler);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        this.tcpClientsToServer!.ForEach(client =>
        {
            client.Stop();
            client.WaitForExit();
        });
        this.tcpClientsToOtherGate!.ForEach(client =>
        {
            client.Stop();
            client.WaitForExit();
        });
        this.hostMgrConnection.ShutDown();
        this.tcpGateServer.Stop();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info("[Startup] STEP 1: connect to host manager.");
        this.hostMgrConnection.Run();
        Logger.Debug("Host manager connected.");

        this.clientsPumpMsgSandBox.Run();
        Logger.Info("[Startup] STEP 2: create gate entity.");
        this.localEntityGeneratedEvent.Wait();
        Logger.Debug($"Gate entity created. {this.entity!.MailBox}");

        var registerCtl = new Control
        {
            From = RemoteType.Gate,
            Message = ControlMessage.Ready,
        };
        registerCtl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
        Logger.Info("[Startup] STEP 3: notify host manager ready.");
        this.hostMgrConnection.Send(registerCtl);

        Logger.Info("[Startup] STEP 4: waiting for synchronizing mailboxes.");
        this.serversMailBoxesReadyEvent.Wait();
        Logger.Info("Servers mailboxes ready.");
        this.otherGatesMailBoxesReadyEvent.Wait();
        Logger.Info("Gates mailboxes ready.");

        Logger.Info("[Startup] STEP 5: Startup gate's tcp server.");
        Logger.Info($"Start gate at {this.Ip}:{this.Port}");
        this.tcpGateServer.Run();

        Logger.Info("[Startup] STEP 6: Start tcp clients to connect to servers and other gates.");
        this.tcpClientsToServer!.ForEach(client => client.Run());
        this.tcpClientsToOtherGate!.ForEach(client => client.Run());

        this.allServersConnectedEvent!.Wait();
        Logger.Info("All servers connected.");

        this.allOtherGatesConnectedEvent!.Wait();
        Logger.Info("All other gates connected.");

        Logger.Info("[Startup] STEP 7: Synchronizing service manager mailbox.");
        this.waitForSyncServiceManagerEvent.Wait();

        Logger.Info("Service manager mailbox got.");
        Logger.Info("Try to connect to service manager");
        this.ConnectToServiceManager();

        Logger.Info("[Startup] STEP 8: Start pumping messages from remote clients.");
        this.readyToPumpClients = true;
        Logger.Info("Waiting completed");

        // NOTE: if tcpClient hash successfully connected to remote, it means remote is already
        // ready to pump message. (tcpServer's OnInit is invoked before tcpServers' Listen)
        Logger.Debug("Try to call Echo method by mailbox");
        this.tcpClientsToServer!.ForEach(client =>
        {
            var serverEntityMailBox = client.MailBox;
            var res = this.entity!.Call(serverEntityMailBox, "Echo", "Hello");
            res.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Logger.Error(t.Exception, $"Echo Res Callback Error, target server mailbox: {serverEntityMailBox}");
                    return;
                }

                Logger.Info("Echo Res Callback");
            });
        });

        // gate main thread will stuck here
        // Array.ForEach(this.tcpClientsToOtherGate!, client => client.WaitForExit());
        this.gateClientsExitEvent!.Wait();

        // Array.ForEach(this.tcpClientsToServer!, client => client.WaitForExit());
        this.serverClientsExitEvent!.Wait();

        this.hostMgrConnection.WaitForExit();
        this.tcpGateServer.WaitForExit();
        this.clientsPumpMsgSandBox.WaitForExit();

        Logger.Debug("Gate Exit.");
    }

    /// <summary>
    /// Update ServerClientEntity related registration in Gate process.
    /// </summary>
    /// <param name="oldMailBox">Old mailbox.</param>
    /// <param name="newMailBox">New mailbox.</param>
    public void UpdateServerClientEntityRegistration(
        Common.Rpc.MailBox oldMailBox,
        Common.Rpc.MailBox newMailBox)
    {
        var oldId = oldMailBox.Id;
        var newId = newMailBox.Id;
        if (!this.entityIdToClientConnMapping.ContainsKey(oldId))
        {
            Logger.Warn("[Gate] Failed to update ServerClientEntity registration.");
            return;
        }

        Logger.Info($"[Gate] Update registration from {oldMailBox} to {newMailBox}");
        this.entityIdToClientConnMapping.Remove(oldId, out var oldInfo);
        this.entityIdToClientConnMapping[newId] = (newMailBox, oldInfo.Connection);
    }

    private static string DecryptedCiphertext(Authentication auth)
    {
        var rsa = RSA.Create();
        var pem = File.ReadAllText("./Config/demo.key").ToCharArray();
        rsa.ImportFromPem(pem);
        var byteData = Convert.FromBase64String(auth.Ciphertext);
        var decryptedBytes = rsa.Decrypt(byteData, RSAEncryptionPadding.Pkcs1);
        var decryptedData = Encoding.UTF8.GetString(decryptedBytes);
        return decryptedData;
    }

    private uint GenerateConnectionId()
    {
        return this.createEntityCounter++;
    }

    private uint GenerateRpcId()
    {
        return this.createEntityCounter++;
    }

    private TcpClient? FindServerTcpClientFromMailBox(MailBox targetMailBox)
    {
        var clientToServer = this.tcpClientsToServer!
            .FirstOrDefault(
                clientToServer => clientToServer?.MailBox.Ip == targetMailBox.IP
                                  && clientToServer.MailBox.Port == targetMailBox.Port
                                  && clientToServer.MailBox.HostNum == targetMailBox.HostNum,
                null);
        return clientToServer;
    }

    private TcpClient? FindServerTcpClientFromMailBox(Common.Rpc.MailBox targetMailBox)
    {
        var clientToServer = this.tcpClientsToServer!
            .FirstOrDefault(
                clientToServer => clientToServer?.MailBox.Ip == targetMailBox.Ip
                                  && clientToServer.MailBox.Port == targetMailBox.Port
                                  && clientToServer.MailBox.HostNum == targetMailBox.HostNum,
                null);
        return clientToServer;
    }

    private void RedirectMsgToClientEntity(string entityId, IMessage msg)
    {
        if (!this.entityIdToClientConnMapping.ContainsKey(entityId))
        {
            Logger.Warn($"{entityId} not exist!");
            return;
        }

        var conn = this.entityIdToClientConnMapping[entityId].Connection;
        var pkg = PackageHelper.FromProtoBuf(msg, 0);
        conn.Socket.Send(pkg.ToBytes());
    }

    private void HandleEntityRpcMessageOnGate(EntityRpc entityRpc)
    {
        var targetEntityMailBox = entityRpc.EntityMailBox!;

        // if rpc's target is gate entity
        if (this.entity!.MailBox.CompareOnlyID(targetEntityMailBox))
        {
            Logger.Debug("send to gate itself");
            Logger.Debug($"Call gate entity: {entityRpc.MethodName}");
            RpcHelper.CallLocalEntity(this.entity, entityRpc);
        }
        else
        {
            var rpcType = entityRpc.RpcType;
            if (rpcType == RpcType.ClientToServer || rpcType == RpcType.ServerInside || rpcType == RpcType.ServiceToEntity)
            {
                // todo: dictionary cache
                var gate = this.tcpClientsToOtherGate!
                    .FirstOrDefault(
                        client => client!.TargetIp == targetEntityMailBox.IP
                                  && client.TargetPort == targetEntityMailBox.Port,
                        null);

                // if rpc's target is other gate entity
                if (gate != null)
                {
                    Logger.Debug("redirect to gate's entity");
                    gate.Send(entityRpc);
                }
                else
                {
                    var serverClient = this.FindServerTcpClientFromMailBox(targetEntityMailBox);
                    if (serverClient != null)
                    {
                        Logger.Debug($"redirect to server {serverClient.MailBox} with rpc type {rpcType}");
                        serverClient.Send(entityRpc);
                    }
                    else
                    {
                        Logger.Warn(
                            $"invalid rpc target mailbox: {targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}" +
                            $"{targetEntityMailBox.HostNum}");
                    }
                }
            }
            else if (rpcType == RpcType.ServerToClient)
            {
                // send to client
                Logger.Info("send rpc to client");
                this.RedirectMsgToClientEntity(entityRpc.EntityMailBox.ID, entityRpc);
            }
            else
            {
                throw new Exception($"Invalid rpc type: {rpcType}");
            }
        }
    }

    private void HandleEntityRpcCallBackMessageOnGate(EntityRpcCallBack callback)
    {
        var targetEntityMailBox = callback.TargetMailBox!;

        // if rpc's target is gate entity
        if (this.entity!.MailBox.CompareOnlyID(targetEntityMailBox))
        {
            Logger.Debug("send to gate itself");
            Logger.Debug($"gate entity rpc callback");
            this.entity.OnRpcCallBack(callback);
        }
        else
        {
            var rpcType = callback.RpcType;
            if (rpcType == RpcType.ClientToServer || rpcType == RpcType.ServerInside)
            {
                // todo: dictionary cache
                var gate = this.tcpClientsToOtherGate!
                    .FirstOrDefault(
                        client => client!.TargetIp == targetEntityMailBox.IP
                                  && client.TargetPort == targetEntityMailBox.Port,
                        null);

                // if rpc's target is other gate entity
                if (gate != null)
                {
                    Logger.Debug("redirect to gate's entity");
                    gate.Send(callback);
                }
                else
                {
                    var serverClient = this.FindServerTcpClientFromMailBox(targetEntityMailBox);
                    if (serverClient != null)
                    {
                        Logger.Debug($"redirect to server {serverClient.MailBox}");
                        serverClient.Send(callback);
                    }
                    else
                    {
                        Logger.Warn(
                            $"invalid rpc target mailbox: {targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}" +
                            $"{targetEntityMailBox.HostNum}");
                    }
                }
            }
            else if (rpcType == RpcType.ServerToClient)
            {
                // send to client
                Logger.Info("send rpc to client");
                this.RedirectMsgToClientEntity(callback.TargetMailBox.ID, callback);
            }
            else if (rpcType == RpcType.EntityToService)
            {
                // send entity rpc msg to service
                Logger.Info("send entity rpc to service");
                this.serviceMgrConnection.Send(callback);
            }
            else
            {
                throw new Exception($"Invalid rpc type: {rpcType}");
            }
        }
    }

    private void PumpMessageHandler()
    {
        try
        {
            while (!this.tcpGateServer.Stopped)
            {
                if (this.readyToPumpClients)
                {
                    foreach (var client in this.tcpClientsToServer!)
                    {
                        client.Pump();
                    }

                    foreach (var client in this.tcpClientsToOtherGate!)
                    {
                        client.Pump();
                    }
                }

                Thread.Sleep(1);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Pump message failed.");
        }

        Logger.Info("Pump message thread exit.");
    }

    private TcpClient RandomServerClient()
    {
        Logger.Debug($"client cnt: {this.tcpClientsToServer!.Count}");
        return this.tcpClientsToServer![this.random.Next(this.tcpClientsToServer.Count)];
    }
}