// -----------------------------------------------------------------------
// <copyright file="HostManager.Register.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc.InnerMessages;

/// <summary>
/// HostManager will watch the status of each component in the host including:
/// Server/Gate/DbManager
/// HostManager use ping/pong strategy to check the status of the components
/// if HostManager find any component looks like dead, it will
/// kick this component off from the host, try to create a new component
/// while writing alert log.
/// </summary>
public partial class HostManager
{
    private void RegisterInstance(RemoteType hostCmdFrom, Common.Rpc.MailBox mailBox, Connection conn)
    {
        conn.MailBox = mailBox;
        this.mailboxIdToConnection[mailBox.Id] = conn;
        this.BroadcastSyncMessage(hostCmdFrom, mailBox);
    }

    private void BroadcastSyncMessage(RemoteType hostCmdFrom, Common.Rpc.MailBox mailBox)
    {
        switch (hostCmdFrom)
        {
            case RemoteType.Gate:
                Logger.Info($"gate require sync {mailBox}");
                lock (this.gatesMailBoxes)
                {
                    this.gatesMailBoxes.Add(mailBox);
                    this.instanceStatusManager.Register(mailBox, InstanceType.Gate);
                }

                break;
            case RemoteType.Server:
                Logger.Info($"server require sync {mailBox}");
                lock (this.serversMailBoxes)
                {
                    this.serversMailBoxes.Add(mailBox);
                    this.instanceStatusManager.Register(mailBox, InstanceType.Server);
                }

                break;
            case RemoteType.ServiceManager:
                this.serviceManagerInfo = (true, mailBox);
                this.instanceStatusManager.Register(mailBox, InstanceType.ServiceManager);
                break;
            case RemoteType.Dbmanager:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmdFrom), hostCmdFrom, null);
        }

        if (this.serversMailBoxes.Count != this.DesiredServerNum || this.gatesMailBoxes.Count != this.DesiredGateNum || !this.serviceManagerInfo.ServicManagerReady)
        {
            return;
        }

        Logger.Info("All gates registered, send sync msg");
        Logger.Info("All servers registered, send sync msg");

        var gateConns = this.mailboxIdToConnection.Values.Where(
                conn => this.gatesMailBoxes.FindIndex(mb => mb.CompareOnlyID(conn.MailBox)) != -1)
            .ToList();
        var serverConns = this.mailboxIdToConnection.Values.Where(
                conn => this.serversMailBoxes.FindIndex(mb => mb.CompareOnlyID(conn.MailBox)) != -1)
            .ToList();

        this.NotifySyncGates(gateConns, serverConns);
        this.NotifySyncServers(gateConns, serverConns);
        this.NotifySyncServiceManager(gateConns, serverConns);

        this.Status = HostStatus.Running;
        this.heartBeatTimer.Change(0, 5000);
    }

    private void NotifySyncGates(List<Connection> gateConns, List<Connection> serverConns)
    {
        // send gates mailboxes
        var syncCmd = new HostCommand
        {
            Type = HostCommandType.SyncGates,
        };
        foreach (var gateConn in this.gatesMailBoxes)
        {
            syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(gateConn)));
        }

        var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Socket.Send(bytes);
        }

        // to server
        foreach (var serverConn in serverConns)
        {
            serverConn.Socket.Send(bytes);
        }

        if (serverConns.Count != this.DesiredServerNum)
        {
            this.messageQueueClientToOtherInstances.Publish(
                bytes,
                Consts.HostMgrToServerExchangeName,
                Consts.HostBroadCastMessagePackageToServer,
                false);
        }

        if (gateConns.Count != this.DesiredGateNum)
        {
            this.messageQueueClientToOtherInstances.Publish(
                bytes,
                Consts.HostMgrToGateExchangeName,
                Consts.HostBroadCastMessagePackageToGate,
                false);
        }
    }

    private void NotifySyncServers(List<Connection> gateConns, List<Connection> serverConns)
    {
        var syncCmd = new HostCommand
        {
            Type = HostCommandType.SyncServers,
        };

        // send server mailboxes
        foreach (var serverConn in this.serversMailBoxes)
        {
            syncCmd.Args.Add(Any.Pack(RpcHelper.RpcMailBoxToPbMailBox(serverConn)));
        }

        var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Socket.Send(bytes);
        }

        if (gateConns.Count != this.DesiredGateNum)
        {
            this.messageQueueClientToOtherInstances.Publish(
                bytes,
                Consts.HostMgrToGateExchangeName,
                Consts.HostBroadCastMessagePackageToGate,
                false);
        }
    }

    private void NotifySyncServiceManager(List<Connection> gateConns, List<Connection> serverConns)
    {
        Logger.Info("notify to sync service manager");

        var syncCmd = new HostCommand
        {
            Type = HostCommandType.SyncServiceManager,
        };

        syncCmd.Args.Add(Any.Pack(new MailBoxArg()
        {
            PayLoad = RpcHelper.RpcMailBoxToPbMailBox(this.serviceManagerInfo.ServiceManagerMailBox),
        }));

        var pkg = PackageHelper.FromProtoBuf(syncCmd, 0);
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Socket.Send(bytes);
        }

        if (gateConns.Count != this.DesiredGateNum)
        {
            this.messageQueueClientToOtherInstances.Publish(
                bytes,
                Consts.HostMgrToGateExchangeName,
                Consts.HostBroadCastMessagePackageToGate,
                false);
        }

        // to server
        foreach (var serverConn in serverConns)
        {
            serverConn.Socket.Send(bytes);
        }

        if (serverConns.Count != this.DesiredServerNum)
        {
            this.messageQueueClientToOtherInstances.Publish(
                bytes,
                Consts.HostMgrToServerExchangeName,
                Consts.HostBroadCastMessagePackageToServer,
                false);
        }
    }
}