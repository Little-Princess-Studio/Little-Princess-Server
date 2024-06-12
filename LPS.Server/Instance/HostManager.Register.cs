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
using MailBox = LPS.Common.Rpc.MailBox;

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
    private void RegisterInstance(
        RemoteType hostCmdFrom,
        Common.Rpc.MailBox mailBox,
        Connection conn)
    {
        conn.MailBox = mailBox;
        this.mailboxIdToConnection[mailBox.Id] = conn;
        this.UpdateInstanceStatus(hostCmdFrom, mailBox);
        this.BroadcastSyncMessage();
    }

    private void UpdateInstanceStatus(RemoteType hostCmdFrom, MailBox mailBox)
    {
        switch (hostCmdFrom)
        {
            case RemoteType.Gate:
                Logger.Info($"gate require sync {mailBox}");
                this.gatesMailBoxes.Add(mailBox);
                this.instanceStatusManager.Register(mailBox, InstanceType.Gate);

                break;
            case RemoteType.Server:
                Logger.Info($"server require sync {mailBox}");
                this.serversMailBoxes.Add(mailBox);
                this.instanceStatusManager.Register(mailBox, InstanceType.Server);

                break;
            case RemoteType.ServiceManager:
                Logger.Info($"service manager ready {mailBox}");
                this.serviceManagerInfo = (true, mailBox);
                this.instanceStatusManager.Register(mailBox, InstanceType.ServiceManager);
                break;
            case RemoteType.Dbmanager:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmdFrom), hostCmdFrom, null);
        }
    }

    private void BroadcastSyncMessage()
    {
        if (this.serversMailBoxes.Count != this.DesiredServerNum || this.gatesMailBoxes.Count != this.DesiredGateNum ||
            !this.serviceManagerInfo.ServicManagerReady)
        {
            Logger.Debug(
                $"server count {this.serversMailBoxes.Count}, gate count {this.gatesMailBoxes.Count}, service manager ready {this.serviceManagerInfo.ServicManagerReady}");
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
        this.NotifySyncServers(gateConns);
        this.NotifySyncServiceManager(gateConns, serverConns);

        this.Status = HostStatus.Running;
        this.heartBeatTimer.Change(1000, 5000);
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
            syncCmd.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(gateConn)));
        }

        var pkg = PackageHelper.FromProtoBuf(syncCmd, ServerGlobal.GenerateRpcId());
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Send(bytes);
        }

        // to server
        foreach (var serverConn in serverConns)
        {
            Logger.Debug($"Sync gates to server {serverConn.MailBox}");
            serverConn.Send(bytes);
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

    private void NotifySyncServers(List<Connection> gateConns)
    {
        var syncCmd = new HostCommand
        {
            Type = HostCommandType.SyncServers,
        };

        // send server mailboxes
        foreach (var serverConn in this.serversMailBoxes)
        {
            syncCmd.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(serverConn)));
        }

        var pkg = PackageHelper.FromProtoBuf(syncCmd, ServerGlobal.GenerateRpcId());
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Send(bytes);
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

        syncCmd.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(this.serviceManagerInfo.ServiceManagerMailBox)));

        var pkg = PackageHelper.FromProtoBuf(syncCmd, ServerGlobal.GenerateRpcId());
        var bytes = pkg.ToBytes();

        // to gates
        foreach (var gateConn in gateConns)
        {
            gateConn.Send(bytes);
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
            serverConn.Send(bytes);
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

    // Handle Restarting
    private void RestartInstance(
        RemoteType hostCmdFrom,
        Common.Rpc.MailBox mailBox,
        Connection conn)
    {
        Logger.Info($"Restarting instance {hostCmdFrom} {mailBox} from immediate connection");
        conn.MailBox = mailBox;
        this.mailboxIdToConnection[mailBox.Id] = conn;

        this.RemoveOldInstanceInfoAndUpdateNewInstanceInfo(
            hostCmdFrom,
            mailBox,
            (mb) => this.mailboxIdToConnection.Remove(mb.Id));
        this.NotifyRestart(hostCmdFrom, mailBox, conn);
    }

    private void RemoveOldInstanceInfoAndUpdateNewInstanceInfo(
        RemoteType hostCmdFrom,
        MailBox mailBox,
        Action<MailBox> onRemoveOldMailBoxInfo)
    {
        // find previous instance's mailbox and remove it
        int index;
        MailBox oldMb;
        switch (hostCmdFrom)
        {
            case RemoteType.Gate:
                index = this.gatesMailBoxes.FindIndex(mb => mb.CompareOnlyAddress(mailBox));
                if (index != -1)
                {
                    oldMb = this.gatesMailBoxes[index];

                    onRemoveOldMailBoxInfo(oldMb);
                    this.instanceStatusManager.Unregister(oldMb);
                    this.gatesMailBoxes.RemoveAt(index);
                }

                this.UpdateInstanceStatus(hostCmdFrom, mailBox);
                break;
            case RemoteType.Server:
                index = this.serversMailBoxes.FindIndex(mb => mb.CompareOnlyAddress(mailBox));
                if (index != -1)
                {
                    oldMb = this.serversMailBoxes[index];

                    onRemoveOldMailBoxInfo(oldMb);
                    this.instanceStatusManager.Unregister(oldMb);
                    this.serversMailBoxes.RemoveAt(index);
                }

                this.UpdateInstanceStatus(hostCmdFrom, mailBox);
                break;
            case RemoteType.ServiceManager:
                oldMb = this.serviceManagerInfo.ServiceManagerMailBox;
                this.instanceStatusManager.Unregister(oldMb);

                onRemoveOldMailBoxInfo(oldMb);
                this.serviceManagerInfo = (true, mailBox);
                this.UpdateInstanceStatus(hostCmdFrom, mailBox);
                break;
            case RemoteType.Dbmanager:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hostCmdFrom), hostCmdFrom, null);
        }
    }

    private void SendSyncCmdToConnection(
        HostCommandType hostCommandType,
        Connection conn,
        List<MailBox> syncMailBoxes)
    {
        var syncCmd = new HostCommand
        {
            Type = hostCommandType,
        };

        foreach (var connMailBox in syncMailBoxes)
        {
            syncCmd.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(connMailBox)));
        }

        var pkg = PackageHelper.FromProtoBuf(syncCmd, ServerGlobal.GenerateRpcId());
        var bytes = pkg.ToBytes();
        conn.Send(bytes);
    }

    private void NotifyRestart(RemoteType hostCmdFrom, Common.Rpc.MailBox mailBox, Connection conn)
    {
        switch (hostCmdFrom)
        {
            // sync gates to restarting server
            case RemoteType.Server:
                // send gates mailboxes
                this.SendSyncCmdToConnection(HostCommandType.SyncGates, conn, this.gatesMailBoxes);
                this.SendSyncCmdToConnection(
                    HostCommandType.SyncServiceManager,
                    conn,
                    [this.serviceManagerInfo.ServiceManagerMailBox]);
                break;

            // sync servers and gates to restarting gate
            case RemoteType.Gate:
            {
                this.SendSyncCmdToConnection(HostCommandType.SyncGates, conn, this.gatesMailBoxes);
                this.SendSyncCmdToConnection(HostCommandType.SyncServers, conn, this.serversMailBoxes);
                this.SendSyncCmdToConnection(
                    HostCommandType.SyncServiceManager,
                    conn,
                    [this.serviceManagerInfo.ServiceManagerMailBox]);
                break;
            }

            default:
                Logger.Warn($"Unsupported restarting type: {hostCmdFrom}");
                break;
        }
    }

    private void NotifyReconnect(RemoteType hostCmdFrom, Common.Rpc.MailBox mailBox)
    {
        switch (hostCmdFrom)
        {
            case RemoteType.Server:
                this.NotifyServersReconnect(mailBox, HostCommandType.ReconnectServer);
                break;
            case RemoteType.Gate:
                this.NotifyGatesReconnect(mailBox, HostCommandType.ReconnectGate);
                break;
            default:
                Logger.Warn($"Unknown hostCmdFrom: {hostCmdFrom}");
                break;
        }
    }

    private void NotifyGatesReconnect(Common.Rpc.MailBox excludedMailBox, HostCommandType hostCommandType)
        => this.NotifyReconnect(excludedMailBox, hostCommandType, this.gatesMailBoxes, RemoteType.Gate);

    private void NotifyServersReconnect(Common.Rpc.MailBox excludedMailBox, HostCommandType hostCommandType)
        => this.NotifyReconnect(excludedMailBox, hostCommandType, this.serversMailBoxes, RemoteType.Server);

    private void NotifyReconnect(Common.Rpc.MailBox excludedMailBox, HostCommandType hostCommandType, List<MailBox> mailBoxes, RemoteType remoteType)
    {
        var syncCmd = new HostCommand
        {
            Type = hostCommandType,
        };

        syncCmd.Args.Add(RpcHelper.GetRpcAny(RpcHelper.RpcMailBoxToPbMailBox(excludedMailBox)));

        // todo: use common Connection instead of identifier/connection
        var pkg = PackageHelper.FromProtoBuf(syncCmd, ServerGlobal.GenerateRpcId());
        var bytes = pkg.ToBytes();

        foreach (var mb in mailBoxes)
        {
            if (mb.CompareOnlyID(excludedMailBox))
            {
                continue;
            }

            if (this.mailboxIdToConnection.TryGetValue(mb.Id, out var conn))
            {
                conn.Send(bytes);
                Logger.Info($"NotifyReconnect {hostCommandType} {excludedMailBox} directly to : {mb}");
            }
            else
            {
                Logger.Warn($"Unknown mailbox {mb}: no connection or identifier is related to it");
            }
        }
    }
}