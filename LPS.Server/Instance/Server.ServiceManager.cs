// -----------------------------------------------------------------------
// <copyright file="Server.ServiceManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance;

using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Instance.HostConnection.HostManagerConnection;
using LPS.Server.Instance.HostConnection.ServiceConnection;

/// <summary>
/// Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
/// redirect to target server instance.
/// </summary>
public partial class Server
{
    private void ConnectToServiceManager(bool isMqConnection)
    {
        if (isMqConnection)
        {
            this.serviceMgrConnection = new MessageQueueServiceManagerConnectionOfServer(this.Name);
        }
        else
        {
            this.serviceMgrConnection = new ImmediateServiceManagerConnectionOfServer(
                this.serviceManagerMailBox.Ip,
                this.serviceManagerMailBox.Port,
                this.GenerateRpcId,
                () => this.tcpServer!.Stopped,
                this.entity!.MailBox);
        }

        this.serviceMgrConnection.RegisterMessageHandler(PackageType.ServiceRpcCallBack, this.HandleServiceRpcCallBack);
        this.serviceMgrConnection.Run();
    }

    private void HandleServiceRpcCallBack(IMessage message)
    {
        var serviceRpcCallBack = (message as ServiceRpcCallBack)!;
        Logger.Debug("HandleServiceRpcCallBack");
        var recieverMailBox = RpcHelper.PbMailBoxToRpcMailBox(serviceRpcCallBack.TargetMailBox);
        if (this.localEntityDict.ContainsKey(recieverMailBox.Id))
        {
            var entity = this.localEntityDict[recieverMailBox.Id];
            entity.OnServiceRpcCallBack(serviceRpcCallBack);
        }
        else
        {
            Logger.Warn("ServiceRpcCallBack target not exist");
        }
    }
}