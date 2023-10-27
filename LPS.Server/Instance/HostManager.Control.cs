// -----------------------------------------------------------------------
// <copyright file="HostManager.Control.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using System;
using System.Collections.Generic;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Server.MessageQueue;

/// <summary>
/// Status of an instance.
/// </summary>
internal enum InstanceStatusType
{
    /// <summary>
    /// The instance is initializing.
    /// </summary>
    Initializing = 0,

    /// <summary>
    /// The instance is running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The instance is dead.
    /// </summary>
    Dead = 2,
}

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
    private sealed class InstanceStatusManager
    {
        private readonly Dictionary<MailBox, InstanceStatus> instanceMap = new();

        public void Register(MailBox mailBox, InstanceType instanceType)
        {
            this.instanceMap.Add(mailBox, new InstanceStatus(instanceType, mailBox));
        }

        public void Unregister(MailBox mailBox)
        {
            this.instanceMap.Remove(mailBox);
        }

        public void UpdateStatus(MailBox mailBox, InstanceStatusType type)
        {
            this.instanceMap[mailBox] = new InstanceStatus(this.instanceMap[mailBox].InstanceType, mailBox)
            {
                Status = type,
            };
        }

        public InstanceStatus GetStatus(MailBox mailBox) => this.instanceMap[mailBox];

        public bool HasInstance(MailBox mailBox) => this.instanceMap.ContainsKey(mailBox);
    }

    private class InstanceStatus
    {
        public readonly InstanceType InstanceType;

        public readonly MailBox MailBox;

        public InstanceStatusType Status { get; set; }

        public DateTime LastHeartBeat { get; set; }

        public bool WaitingForPong { get; set; }

        public InstanceStatus(InstanceType instanceType, MailBox mailBox)
        {
            this.InstanceType = instanceType;
            this.MailBox = mailBox;
            this.Status = InstanceStatusType.Initializing;
            this.LastHeartBeat = DateTime.Now;
        }

        public bool CouldBeTaggedAsDead()
        {
            if (this.Status == InstanceStatusType.Dead)
            {
                return true;
            }

            // if (this.Status == InstanceStatusType.Initializing)
            // {
            //     return DateTime.UtcNow - this.LastHeartBeat > TimeSpan.FromSeconds(10);
            // }
            return DateTime.UtcNow - this.LastHeartBeat > TimeSpan.FromSeconds(30);
        }
    }

    private void HeartBeatDetect()
    {
        if (this.Status != HostStatus.Running)
        {
            return;
        }

        Logger.Debug("[Ping] Start ping");

        var ping = new Common.Rpc.InnerMessages.Ping();
        var pkg = Common.Rpc.InnerMessages.PackageHelper.FromProtoBuf(ping, 0);
        var bytes = pkg.ToBytes();

        // broadcast to servers
        foreach (var mb in this.serversMailBoxes)
        {
            this.CheckInstanceByMailBoxAndSendPingMessage(
            in mb,
            bytes,
            Consts.HostMgrToServerExchangeName,
            identifier => Consts.GenerateHostMessageToServerPackage(identifier));
        }

        // broadcast to gates
        foreach (var mb in this.gatesMailBoxes)
        {
            this.CheckInstanceByMailBoxAndSendPingMessage(
            in mb,
            bytes,
            Consts.HostMgrToGateExchangeName,
            identifier => Consts.GenerateHostMessageToGatePackage(identifier));
        }

        // detect service manager
        var serviceMgrMb = this.serviceManagerInfo.ServiceManagerMailBox;
        this.CheckInstanceByMailBoxAndSendPingMessage(
        in serviceMgrMb,
        bytes,
        Consts.HostMgrToServiceMgrExchangeName,
        _ => Consts.ServiceMgrMessagePackage);
    }

    private void CheckInstanceByMailBoxAndSendPingMessage(in Common.Rpc.MailBox mailBox, byte[] bytesToSend, string exchange, Func<string, string> getRoutingKey)
    {
        var id = mailBox.Id;

        if (!this.instanceStatusManager.HasInstance(mailBox))
        {
            return;
        }

        InstanceStatus? status = this.instanceStatusManager.GetStatus(mailBox);

        if (status.WaitingForPong)
        {
            // if the instance is still waiting for pong and no pong has been received, tagged as dead.
            if (status.CouldBeTaggedAsDead())
            {
                this.instanceStatusManager.UpdateStatus(mailBox, InstanceStatusType.Dead);
            }

            return;
        }

        if (this.mailboxIdToConnection.TryGetValue(id, out var serviceMgrConn))
        {
            status.WaitingForPong = true;
            serviceMgrConn.Socket.Send(bytesToSend);
        }
        else if (this.mailboxIdToIdentifier.TryGetValue(id, out var identifier))
        {
            status.WaitingForPong = true;
            this.messageQueueClientToOtherInstances.Publish(
                    bytesToSend,
                    exchange,
                    getRoutingKey.Invoke(identifier));
        }
        else
        {
            Logger.Warn($"Cannot find connection or identifier for mailbox with {id}");
        }
    }

    private void HandlePongMessage(Common.Rpc.InnerMessages.Pong pong)
    {
        var mb = RpcHelper.PbMailBoxToRpcMailBox(pong.SenderMailBox);
        if (!this.instanceStatusManager.HasInstance(mb))
        {
            Logger.Warn($"[Pong] Cannot find instance with mailbox {mb}");
            return;
        }

        var status = this.instanceStatusManager.GetStatus(mb);
        if (status.Status == InstanceStatusType.Initializing)
        {
            this.instanceStatusManager.UpdateStatus(mb, InstanceStatusType.Running);
        }

        status.WaitingForPong = false;
        status.LastHeartBeat = DateTime.UtcNow;
    }
}