// -----------------------------------------------------------------------
// <copyright file="Gate.HostManagerMessages.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Rpc;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// Each gate need maintain multiple connections from remote clients
/// and maintain a connection to hostmanager.
/// For hostmanager, gate is a client
/// for remote clients, gate is a server.
/// All the gate mailbox info will be saved in redis, and gate will
/// repeatly sync these info from redis.
/// </summary>
public partial class Gate
{
    private void InitHostManagerConnection(bool useMqToHostMgr, string hostManagerIp, int hostManagerPort)
    {
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
    }

    private void HandleHostCommandFromHost(IMessage msg)
    {
        var hostCmd = (msg as HostCommand)!;

        Logger.Info($"Handle host command, cmd type: {hostCmd.Type}");

        switch (hostCmd.Type)
        {
            case HostCommandType.SyncServers:
                this.SyncServersMailBoxes(hostCmd.Args
                    .Select(mb => RpcHelper.PbMailBoxToRpcMailBox(mb.Unpack<Common.Rpc.InnerMessages.MailBox>())).ToArray());
                break;
            case HostCommandType.SyncGates:
                this.SyncOtherGatesMailBoxes(hostCmd.Args
                    .Select(mb => RpcHelper.PbMailBoxToRpcMailBox(mb.Unpack<Common.Rpc.InnerMessages.MailBox>())).ToArray());
                break;
            case HostCommandType.SyncServiceManager:
                this.SyncServiceManagerMailBox(RpcHelper.PbMailBoxToRpcMailBox(hostCmd.Args[0].Unpack<MailBoxArg>().PayLoad));
                break;
            case HostCommandType.Open:
                break;
            case HostCommandType.Stop:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
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
                    var clientToServer = this.FindServerTcpClientFromMailBox(targetMailBox);
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
                OnSendEntityRpcCallback = entityRpcCallback =>
                {
                    var targetMailBox = entityRpcCallback.TargetMailBox;
                    var clientToServer = this.FindServerTcpClientFromMailBox(targetMailBox);
                    if (clientToServer != null)
                    {
                        Logger.Debug($"Rpc Call, send entityRpcCallback to {targetMailBox}");
                        clientToServer.Send(entityRpcCallback);
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
                    this.NotifyServerReady(self);
                    this.allServersConnectedEvent.Signal();
                },
                MailBox = mb,
            };
            this.tcpClientsToServer[idx] = client;
            ++idx;
        }

        this.serversMailBoxesReadyEvent.Signal();
    }

    private void NotifyServerReady(TcpClient clientToServer)
    {
        var ctl = new Control()
        {
            From = RemoteType.Gate,
            Message = ControlMessage.Ready,
        };

        ctl.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(this.entity!.MailBox)));
        clientToServer.Send(ctl, false);
    }

    private void SyncServiceManagerMailBox(Common.Rpc.MailBox serviceManagerMailBox)
    {
        this.waitForSyncServiceManagerEvent.Signal();
        this.serviceManagerMailBox = serviceManagerMailBox;
    }
}