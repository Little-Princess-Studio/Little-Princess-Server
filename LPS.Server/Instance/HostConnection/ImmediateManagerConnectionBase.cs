// -----------------------------------------------------------------------
// <copyright file="ImmediateManagerConnectionBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using System.Threading;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Server.Rpc;

/// <summary>
/// Use socket to connect to host manager.
/// </summary>
internal abstract class ImmediateManagerConnectionBase : IManagerConnection
{
    /// <summary>
    /// Dispatcher to dispatch message.
    /// </summary>
    protected readonly Dispatcher<IMessage> MsgDispatcher = new();

    /// <summary>
    /// Countdown event to signal when the connection to the host manager is established.
    /// </summary>
    protected readonly CountdownEvent ManagerConnectedEvent;

    private readonly SandBox clientsPumpMsgSandBox;
    private readonly Func<bool> checkServerStopped;

    private TcpClient clientToManager = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateManagerConnectionBase"/> class.
    /// </summary>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    protected ImmediateManagerConnectionBase(Func<bool> checkServerStopped)
    {
        this.checkServerStopped = checkServerStopped;

        this.ManagerConnectedEvent = new CountdownEvent(1);
        this.clientsPumpMsgSandBox = SandBox.Create(this.PumpMessageHandler);
    }

    /// <inheritdoc/>
    public void Run()
    {
        this.clientToManager = this.GetTcpClient();
        this.clientToManager.Run();
        this.ManagerConnectedEvent.Wait();
        this.BeforeStartPumpMessage();
        this.clientsPumpMsgSandBox.Run();
    }

    /// <inheritdoc/>
    public void ShutDown()
    {
        this.clientToManager.Stop();
    }

    /// <inheritdoc/>
    public void WaitForExit()
    {
        this.clientsPumpMsgSandBox.WaitForExit();
        this.clientToManager.WaitForExit();
    }

    /// <inheritdoc/>
    public void Send(IMessage message)
    {
        this.clientToManager.Send(message);
    }

    /// <inheritdoc/>
    public void RegisterMessageHandler(PackageType packageType, Action<IMessage> handler)
    {
        this.MsgDispatcher.Register(packageType, handler);
    }

    /// <inheritdoc/>
    public void UnregisterMessageHandler(PackageType packageType, Action<IMessage> handler)
    {
        this.MsgDispatcher.Register(packageType, handler);
    }

    /// <summary>
    /// Get tcp client to host manager.
    /// </summary>
    /// <returns>Tcp client.</returns>
    protected abstract TcpClient GetTcpClient();

    /// <summary>
    /// Callback before start pump message.
    /// </summary>
    protected virtual void BeforeStartPumpMessage()
    {
    }

    /// <summary>
    /// Generic method to handle message from host.
    /// </summary>
    /// <param name="arg">Message.</param>
    /// <typeparam name="TPackage">Protobuf package type.</typeparam>
    protected void HandleMessageFromManager<TPackage>((IMessage Message, Connection Connection, uint RpcId) arg)
        where TPackage : IMessage
    {
        var (msg, _, _) = arg;
        this.MsgDispatcher.Dispatch(PackageHelper.GetPackageType<TPackage>(), msg);
    }

    private void PumpMessageHandler()
    {
        try
        {
            while (!this.checkServerStopped.Invoke())
            {
                this.clientToManager.Pump();
                Thread.Sleep(1);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Pump message failed.");
        }
    }
}