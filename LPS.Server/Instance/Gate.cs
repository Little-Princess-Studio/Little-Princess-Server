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
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;
using MailBox = LPS.Common.Rpc.InnerMessages.MailBox;

/// <summary>
/// Each gate need maintain multiple connections from remote clients
/// and maintain a connection to hostmanager.
/// For hostmanager, gate is a client
/// for remote clients, gate is a server.
/// All the gate mailbox info will be saved in redis, and gate will
/// repeatly sync these info from redis.
/// </summary>
public class Gate : IInstance
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

    private readonly CountdownEvent localEntityGeneratedEvent;

    private readonly SandBox clientsPumpMsgSandBox;

    private readonly TcpServer tcpGateServer;
    private readonly IManagerConnection hostConnection;

    private GateEntity? entity;

    // if all the tcp clients have connected to server/other gate, countdownEvent_ will down to 0
    private CountdownEvent? allServersConnectedEvent;
    private CountdownEvent? allOtherGatesConnectedEvent;

    private uint createEntityCounter;

    private TcpClient[]? tcpClientsToServer;
    private TcpClient[]? tcpClientsToOtherGate;

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
    public Gate(
        string name,
        string ip,
        int port,
        int hostNum,
        string hostManagerIp,
        int hostManagerPort,
        (string IP, int Port)[] servers,
        (string InnerIp, string Ip, int Port)[] otherGates,
        bool useMqToHostMgr)
    {
        this.Name = name;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;

        // tcp gate server handles msg from server/other gates
        this.tcpGateServer = new TcpServer(ip, port)
        {
            OnInit = this.RegisterMessageFromServerAndOtherGateHandlers,
            OnDispose = this.UnregisterMessageFromServerAndOtherGateHandlers,
        };

        this.localEntityGeneratedEvent = new CountdownEvent(1);

        if (!useMqToHostMgr)
        {
            this.hostConnection = new ImmediateHostManagerConnectionOfGate(
                hostManagerIp,
                hostManagerPort,
                this.GenerateRpcId,
                () => this.tcpGateServer.Stopped);
        }
        else
        {
            this.hostConnection = new MessageQueueHostManagerConnectionOfGate(this.Name, this.GenerateRpcId);
        }

        this.hostConnection.RegisterMessageHandler(
            PackageType.RequireCreateEntityRes,
            this.HandleRequireCreateEntityResFromHost);
        this.hostConnection.RegisterMessageHandler(PackageType.HostCommand, this.HandleHostCommandFromHost);

        this.clientsPumpMsgSandBox = SandBox.Create(this.PumpMessageHandler);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        Array.ForEach(this.tcpClientsToServer!, client => client.Stop());
        Array.ForEach(this.tcpClientsToOtherGate!, client => client.Stop());
        this.hostConnection.ShutDown();
        this.tcpGateServer.Stop();
    }

    /// <inheritdoc/>
    public void Loop()
    {
        Logger.Info($"Start gate at {this.Ip}:{this.Port}");
        this.tcpGateServer.Run();
        this.hostConnection.Run();

        Logger.Debug("Host manager connected.");

        this.clientsPumpMsgSandBox.Run();
        this.localEntityGeneratedEvent.Wait();
        Logger.Debug($"Gate entity created. {this.entity!.MailBox}");

        var registerCtl = new Control
        {
            From = RemoteType.Gate,
            Message = ControlMessage.Ready,
        };

        registerCtl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
        this.hostConnection.Send(registerCtl);

        this.serversMailBoxesReadyEvent.Wait();
        Logger.Info("Servers mailboxes ready.");
        this.otherGatesMailBoxesReadyEvent.Wait();
        Logger.Info("Gates mailboxes ready.");

        Array.ForEach(this.tcpClientsToServer!, client => client.Run());
        Array.ForEach(this.tcpClientsToOtherGate!, client => client.Run());

        this.allServersConnectedEvent!.Wait();
        Logger.Info("All servers connected.");

        this.allOtherGatesConnectedEvent!.Wait();
        Logger.Info("All other gates connected.");

        this.readyToPumpClients = true;
        Logger.Info("Waiting completed");

        // NOTE: if tcpClient hash successfully connected to remote, it means remote is already
        // ready to pump message. (tcpServer's OnInit is invoked before tcpServers' Listen)
        Logger.Debug("Try to call Echo method by mailbox");
        Array.ForEach(this.tcpClientsToServer!, client =>
        {
            var serverEntityMailBox = client.MailBox;
            var res = this.entity!.Call(serverEntityMailBox, "Echo", "Hello");
            res.ContinueWith(t => Logger.Info($"Echo Res Callback"));
        });

        // gate main thread will stuck here
        Array.ForEach(this.tcpClientsToOtherGate!, client => client.WaitForExit());
        Array.ForEach(this.tcpClientsToServer!, client => client.WaitForExit());
        this.hostConnection.WaitForExit();
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
        LPS.Common.Rpc.MailBox oldMailBox,
        LPS.Common.Rpc.MailBox newMailBox)
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

    #region register server message

    private void RegisterMessageFromServerAndOtherGateHandlers()
    {
        this.tcpGateServer.RegisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);

        // tcpGateServer_.RegisterMessageHandler(PackageType.Control, this.HandleControlMessage);
        this.tcpGateServer.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromClient);
        this.tcpGateServer.RegisterMessageHandler(
            PackageType.RequirePropertyFullSync,
            this.HandleRequireFullSyncFromClient);
        this.tcpGateServer.RegisterMessageHandler(
            PackageType.RequireComponentSync,
            this.HandleRequireComponentSyncFromClient);
    }

    private void UnregisterMessageFromServerAndOtherGateHandlers()
    {
        this.tcpGateServer.UnregisterMessageHandler(
            PackageType.Authentication,
            this.HandleAuthenticationFromClient);

        // tcpGateServer_.UnregisterMessageHandler(PackageType.Control, this.HandleControlMessage);
        this.tcpGateServer.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromClient);
        this.tcpGateServer.UnregisterMessageHandler(
            PackageType.RequirePropertyFullSync,
            this.HandleRequireFullSyncFromClient);
        this.tcpGateServer.UnregisterMessageHandler(
            PackageType.RequireComponentSync,
            this.HandleRequireComponentSyncFromClient);
    }

    private void HandleHostCommandFromHost(IMessage msg)
    {
        var hostCmd = (msg as HostCommand)!;

        Logger.Info($"Handle host command, cmd type: {hostCmd.Type}");

        switch (hostCmd.Type)
        {
            case HostCommandType.SyncServers:
                this.SyncServersMailBoxes(hostCmd.Args
                    .Select(mb => RpcHelper.PbMailBoxToRpcMailBox(mb.Unpack<MailBox>())).ToArray());
                break;
            case HostCommandType.SyncGates:
                this.SyncOtherGatesMailBoxes(hostCmd.Args
                    .Select(mb => RpcHelper.PbMailBoxToRpcMailBox(mb.Unpack<MailBox>())).ToArray());
                break;
            case HostCommandType.Open:
                break;
            case HostCommandType.Stop:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void SyncOtherGatesMailBoxes(Common.Rpc.MailBox[] otherGatesMailBoxes)
    {
        // connect to each gate
        // tcp gate to other gate only send msg to other gate's server
        Logger.Info($"Sync gates, cnt: {otherGatesMailBoxes.Length}");
        this.allOtherGatesConnectedEvent = new CountdownEvent(otherGatesMailBoxes.Length - 1);
        this.tcpClientsToOtherGate = new Rpc.TcpClient[otherGatesMailBoxes.Length - 1];
        var idx = 0;
        foreach (var mb in otherGatesMailBoxes)
        {
            if (mb.CompareOnlyAddress(this.entity!.MailBox))
            {
                continue;
            }

            var tmpIdx = idx;
            var otherGateInnerIp = mb.Ip;
            var otherGatePort = mb.Port;
            var client = new TcpClient(otherGateInnerIp, otherGatePort, this.sendQueue)
            {
                OnConnected = _ => this.allOtherGatesConnectedEvent.Signal(),
                MailBox = mb,
            };

            this.tcpClientsToOtherGate[idx] = client;
            ++idx;
        }

        this.otherGatesMailBoxesReadyEvent.Signal();
    }

    private void SyncServersMailBoxes(Common.Rpc.MailBox[] serverMailBoxes)
    {
        // connect to each server
        // tcp gate to server handlers msg from server
        Logger.Info($"Sync servers, cnt: {serverMailBoxes.Length}");
        this.allServersConnectedEvent = new(serverMailBoxes.Length);
        this.tcpClientsToServer = new TcpClient[serverMailBoxes.Length];
        var idx = 0;
        foreach (var mb in serverMailBoxes)
        {
            var tmpIdx = idx;
            var serverIp = mb.Ip;
            var serverPort = mb.Port;
            Logger.Debug($"server ip: {serverIp} server port: {serverPort}");
            var client = new TcpClient(serverIp, serverPort, this.sendQueue)
            {
                OnInit = _ => this.RegisterGateMessageHandlers(tmpIdx),
                OnDispose = _ => this.UnregisterGateMessageHandlers(tmpIdx),
                OnConnected = self =>
                {
                    var ctl = new Control()
                    {
                        From = RemoteType.Gate,
                        Message = ControlMessage.Ready,
                    };

                    ctl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
                    self.Send(ctl, false);

                    this.allServersConnectedEvent.Signal();
                },
                MailBox = mb,
            };
            this.tcpClientsToServer[idx] = client;
            ++idx;
        }

        this.serversMailBoxesReadyEvent.Signal();
    }

    private void HandleEntityRpcFromClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        // if gate's server have recieved the EntityRpc msg, it must be redirect from other gates
        Logger.Info("Handle EntityRpc From Other Gates.");

        var (msg, _, _) = arg;
        var entityRpc = (msg as EntityRpc)!;
        this.HandleEntityRpcMessageOnGate(entityRpc);
    }

    private uint GenerateRpcId()
    {
        return this.createEntityCounter++;
    }

    private void HandleAuthenticationFromClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, conn, _) = arg;
        var auth = (msg as Authentication)!;

        // TODO: Cache the rsa object
        var decryptedData = DecryptedCiphertext(auth);

        Logger.Info($"Got decrypted content: {decryptedData}");

        if (decryptedData == auth.Content)
        {
            Logger.Info("Auth success");

            var connId = this.GenerateRpcId();

            var createEntityMsg = new RequireCreateEntity
            {
                EntityClassName = "Untrusted",
                CreateType = CreateType.Anywhere,
                Description = string.Empty,
                EntityType = EntityType.ServerClientEntity,
                ConnectionID = connId,
                GateId = this.entity!.MailBox.Id,
            };

            if (conn.ConnectionId != uint.MaxValue)
            {
                throw new Exception("Entity is creating");
            }

            conn.ConnectionId = connId;
            this.createEntityMapping[connId] = conn;

            this.hostConnection.Send(createEntityMsg);
        }
        else
        {
            Logger.Warn("Auth failed");
            conn.Disconnect();
            conn.TokenSource.Cancel();
        }
    }

    private void HandleRequireFullSyncFromClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("[Gate] HandleRequireFullSyncFromClient");
        var (msg, conn, _) = arg;
        var requirePropertyFullSyncMsg = (msg as RequirePropertyFullSync)!;

        this.RedirectMsgToEntityOnServer(requirePropertyFullSyncMsg.EntityId, msg);
    }

    private void HandleRequireComponentSyncFromClient((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("[Gate] HandleRequireComponentSyncFromClient");
        var (msg, conn, _) = arg;
        var requireComponentSyncMsg = (msg as RequireComponentSync)!;

        this.RedirectMsgToEntityOnServer(requireComponentSyncMsg.EntityId, msg);
    }

    #endregion

    #region register client message

    private void RegisterGateMessageHandlers(int serverIdx)
    {
        var client = this.tcpClientsToServer![serverIdx];

        void EntityRpcHandler((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleEntityRpcFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.EntityRpc)] = EntityRpcHandler;

        void PropertyFullSync((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandlePropertyFullSyncFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.PropertyFullSync)] = PropertyFullSync;

        void PropSyncCommandList((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandlePropertySyncCommandListFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.PropertySyncCommandList)] = PropSyncCommandList;

        void ComponentSync((IMessage Message, Connection Connection, uint RpcId) arg) =>
            this.HandleComponentSyncFromServer(client, arg);
        this.tcpClientsActions[(serverIdx, PackageType.ComponentSync)] = ComponentSync;

        client.RegisterMessageHandler(PackageType.EntityRpc, EntityRpcHandler);
        client.RegisterMessageHandler(PackageType.PropertyFullSync, PropertyFullSync);
        client.RegisterMessageHandler(PackageType.PropertySyncCommandList, PropSyncCommandList);
        client.RegisterMessageHandler(PackageType.ComponentSync, ComponentSync);

        Logger.Info($"client {serverIdx} registered msg");
    }

    private void UnregisterGateMessageHandlers(int idx)
    {
        var client = this.tcpClientsToServer![idx];

        client.UnregisterMessageHandler(
            PackageType.EntityRpc,
            this.tcpClientsActions[(idx, PackageType.EntityRpc)]);

        client.UnregisterMessageHandler(
            PackageType.PropertyFullSync,
            this.tcpClientsActions[(idx, PackageType.PropertyFullSync)]);

        client.UnregisterMessageHandler(
            PackageType.PropertySyncCommandList,
            this.tcpClientsActions[(idx, PackageType.PropertySyncCommandList)]);

        client.UnregisterMessageHandler(
            PackageType.ComponentSync,
            this.tcpClientsActions[(idx, PackageType.ComponentSync)]);
    }

    private void HandlePropertySyncCommandListFromServer(
        TcpClient client,
        (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var propertySyncCommandList = (msg as PropertySyncCommandList)!;

        Logger.Info($"property sync: {propertySyncCommandList.Path}" +
                    $" {propertySyncCommandList.EntityId}" +
                    $" {propertySyncCommandList.PropType}");

        // TODO: Redirect to shadow entity on server
        this.RedirectMsgToEntityOnClient(propertySyncCommandList.EntityId, propertySyncCommandList);
    }

    private void HandleComponentSyncFromServer(TcpClient client, (IMessage Message, Connection Connection, uint RpcId) arg)
    {
        Logger.Info("HandleComponentSyncFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var componentSync = (msg as ComponentSync)!;

        Logger.Info("send componentSync to client");
        this.RedirectMsgToEntityOnClient(componentSync.EntityId, msg);
    }

    private void HandleRequireCreateEntityResFromHost(IMessage msg)
    {
        var createEntityRes = (msg as RequireCreateEntityRes)!;

        Logger.Debug($"HandleRequireCreateEntityResFromHost {createEntityRes.Mailbox}");

        if (createEntityRes.EntityType == EntityType.GateEntity)
        {
            Logger.Info("Create gate entity success.");
            var serverEntityMailBox =
                new Common.Rpc.MailBox(createEntityRes.Mailbox.ID, this.Ip, this.Port, this.HostNum);
            this.entity = new(serverEntityMailBox, this)
            {
                OnSendEntityRpc = entityRpc =>
                {
                    var targetMailBox = entityRpc.EntityMailBox;
                    var clientToServer = this.FindServerOfEntity(targetMailBox);
                    if (clientToServer != null)
                    {
                        Logger.Debug($"Rpc Call, send entityRpc to {targetMailBox}");
                        clientToServer.Send(entityRpc);
                    }
                    else
                    {
                        throw new Exception($"gate's server client not found: {targetMailBox}");
                    }
                },
            };

            this.localEntityGeneratedEvent.Signal(1);
        }
        else if (createEntityRes.EntityType == EntityType.ServerClientEntity)
        {
            Logger.Info($"Create server client entity {createEntityRes.EntityClassName}");
            var connId = createEntityRes.ConnectionID;
            if (!this.createEntityMapping.ContainsKey(connId))
            {
                Logger.Warn($"Invalid connid: {connId}");
                return;
            }

            this.createEntityMapping.Remove(connId, out var connToClient);
            var mb = RpcHelper.PbMailBoxToRpcMailBox(createEntityRes.Mailbox!);

            Logger.Info($"create server client entity {mb.Id}");
            this.entityIdToClientConnMapping[mb.Id] = (mb, connToClient!);
            var clientCreateEntity = new ClientCreateEntity
            {
                EntityClassName = createEntityRes.EntityClassName,
                ServerClientMailBox = createEntityRes.Mailbox,
            };
            var pkg = PackageHelper.FromProtoBuf(clientCreateEntity, 0);
            connToClient!.Socket.Send(pkg.ToBytes());
        }
    }

    private TcpClient? FindServerOfEntity(MailBox targetMailBox)
    {
        var clientToServer = this.tcpClientsToServer!
            .FirstOrDefault(
                clientToServer => clientToServer?.MailBox.Ip == targetMailBox.IP
                                  && clientToServer.MailBox.Port == targetMailBox.Port
                                  && clientToServer.MailBox.HostNum == targetMailBox.HostNum,
                null);
        return clientToServer;
    }

    private TcpClient? FindServerOfEntity(Common.Rpc.MailBox targetMailBox)
    {
        var clientToServer = this.tcpClientsToServer!
            .FirstOrDefault(
                clientToServer => clientToServer?.MailBox.Ip == targetMailBox.Ip
                                  && clientToServer.MailBox.Port == targetMailBox.Port
                                  && clientToServer.MailBox.HostNum == targetMailBox.HostNum,
                null);
        return clientToServer;
    }

    private void HandleEntityRpcFromServer(TcpClient client, object arg)
    {
        Logger.Info("HandleEntityRpcFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var entityRpc = (msg as EntityRpc)!;
        this.HandleEntityRpcMessageOnGate(entityRpc);
    }

    private void HandlePropertyFullSyncFromServer(TcpClient client, object arg)
    {
        Logger.Info("HandlePropertyFullSyncFromServer");

        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var fullSync = (msg as PropertyFullSync)!;

        Logger.Info("send fullSync to client");
        this.RedirectMsgToEntityOnClient(fullSync.EntityId, msg);
    }

    #endregion

    private void RedirectMsgToEntityOnServer(string entityId, IMessage msg)
    {
        if (!this.entityIdToClientConnMapping.ContainsKey(entityId))
        {
            Logger.Warn($"{entityId} not exist!");
            return;
        }

        var mb = this.entityIdToClientConnMapping[entityId].MailBox;
        var clientToServer = this.FindServerOfEntity(mb);

        if (clientToServer != null)
        {
            clientToServer.Send(msg, false);
        }
        else
        {
            Logger.Warn($"gate's server client not found: {entityId}");
        }
    }

    private void RedirectMsgToEntityOnClient(string entityId, IMessage msg)
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
                    gate.Send(entityRpc);
                }
                else
                {
                    var serverClient = this.FindServerOfEntity(targetEntityMailBox);
                    if (serverClient != null)
                    {
                        Logger.Debug($"redirect to server {serverClient.MailBox}");
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
                this.RedirectMsgToEntityOnClient(entityRpc.EntityMailBox.ID, entityRpc);
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
    }

    private TcpClient RandomServerClient()
    {
        Logger.Debug($"client cnt: {this.tcpClientsToServer!.Length}");
        return this.tcpClientsToServer![this.random.Next(this.tcpClientsToServer.Length)];
    }
}