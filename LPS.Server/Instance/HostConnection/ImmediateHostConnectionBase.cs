// -----------------------------------------------------------------------
// <copyright file="ImmediateHostConnectionBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Instance.HostConnection;

using System;
using System.Collections.Concurrent;
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
internal abstract class ImmediateHostConnectionBase : IHostConnection
{
    /// <summary>
    /// Gets tcp client to host manager.
    /// </summary>
    protected TcpClient ClientToHostManager { get; private set; } = null!;

    /// <summary>
    /// Dispatcher to dispatch message.
    /// </summary>
    protected readonly Dispatcher<IMessage> MsgDispatcher = new Dispatcher<IMessage>();

    private readonly CountdownEvent hostManagerConnectedEvent;
    private readonly SandBox clientsPumpMsgSandBox;
    private readonly Func<bool> checkServerStopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateHostConnectionBase"/> class.
    /// </summary>
    /// <param name="checkServerStopped">Check if server stopped.</param>
    public ImmediateHostConnectionBase(Func<bool> checkServerStopped)
    {
        this.checkServerStopped = checkServerStopped;
        this.hostManagerConnectedEvent = new CountdownEvent(1);
        this.clientsPumpMsgSandBox = SandBox.Create(this.PumpMessageHandler);
    }

    /// <inheritdoc/>
    public void Run()
    {
        this.ClientToHostManager = this.GetTcpClient();
        this.ClientToHostManager.Run();
        this.BeforeStartPumpMessage();
        this.clientsPumpMsgSandBox.Run();
    }

    /// <inheritdoc/>
    public void ShutDown()
    {
        this.ClientToHostManager.Stop();
    }

    /// <inheritdoc/>
    public void WaitForExit()
    {
        this.clientsPumpMsgSandBox.WaitForExit();
        this.ClientToHostManager.WaitForExit();
    }

    /// <inheritdoc/>
    public void Send(IMessage message)
    {
        this.ClientToHostManager.Send(message);
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
    protected abstract void BeforeStartPumpMessage();

    /// <summary>
    /// Generic method to handle message from host.
    /// </summary>
    /// <param name="arg">Message.</param>
    /// <typeparam name="TPackage">Protobuf package type.</typeparam>
    protected void HandleMessageFromHost<TPackage>((IMessage Message, Connection Connection, uint RpcId) arg)
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
                this.ClientToHostManager.Pump();
                Thread.Sleep(1);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Pump message failed.");
        }
    }
}