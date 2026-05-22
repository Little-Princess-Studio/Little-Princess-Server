// -----------------------------------------------------------------------
// <copyright file="KcpConnection.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System;
using Google.Protobuf;
using kcp2k;
using LPS.Common.Debug;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// <see cref="Connection"/> backed by a kcp2k peer. Two flavours - server
/// uses <see cref="ForServer"/> wrapping (KcpServer + connId), client uses
/// <see cref="ForClient"/> wrapping a KcpClient. Both flavours forward
/// <see cref="Send"/> through kcp2k's reliable channel; <see cref="Disconnect"/>
/// asks kcp2k to close the peer.
/// <para>
/// Unlike <see cref="SocketConnection"/>, KCP is message-oriented: each
/// <c>kcp2k.Send</c> arrives as exactly one <c>OnData</c> on the peer. The
/// LPS framing layer (<see cref="Package"/> + <see cref="MessageBuffer"/>)
/// is not used on the receive side because we have no byte stream to
/// reassemble - we hand the whole datagram to <see cref="PackageHelper"/>
/// directly. The Send side still produces the same on-wire bytes for
/// protocol-symmetric debugging.
/// </para>
/// </summary>
public sealed class KcpConnection : Connection
{
    private readonly Action<ArraySegment<byte>> sendImpl;
    private readonly Action disconnectImpl;

    private KcpConnection(Action<ArraySegment<byte>> sendImpl, Action disconnectImpl)
    {
        this.Status = ConnectStatus.Init;
        this.sendImpl = sendImpl;
        this.disconnectImpl = disconnectImpl;
    }

    /// <summary>
    /// Build a server-side connection wrapper. Each accepted client gets one
    /// of these; sends go through <see cref="KcpServer.Send"/> targeted at the
    /// kcp2k-assigned <paramref name="connectionId"/>.
    /// </summary>
    /// <param name="server">Underlying kcp2k server instance.</param>
    /// <param name="connectionId">kcp2k connection id (hash of remote endpoint).</param>
    /// <returns>A new <see cref="KcpConnection"/>.</returns>
    public static KcpConnection ForServer(KcpServer server, int connectionId)
    {
        return new KcpConnection(
            sendImpl: bytes => server.Send(connectionId, bytes, KcpChannel.Reliable),
            disconnectImpl: () => server.Disconnect(connectionId));
    }

    /// <summary>
    /// Build a client-side connection wrapper. Sends go through
    /// <see cref="KcpClient.Send"/>; disconnect closes the peer.
    /// </summary>
    /// <param name="client">Underlying kcp2k client instance.</param>
    /// <returns>A new <see cref="KcpConnection"/>.</returns>
    public static KcpConnection ForClient(KcpClient client)
    {
        return new KcpConnection(
            sendImpl: bytes => client.Send(bytes, KcpChannel.Reliable),
            disconnectImpl: client.Disconnect);
    }

    /// <inheritdoc/>
    public override void Disconnect()
    {
        try
        {
            Logger.Debug("[KcpConnection] Disconnect invoked.");
            this.Status = ConnectStatus.Disconnected;
            this.disconnectImpl();
            this.OnDisconnected?.Invoke();
        }
        catch (Exception e)
        {
            Logger.Error(e, "[KcpConnection] OnDisconnected handler exception.");
        }
    }

    /// <inheritdoc/>
    public override void Send(IMessage message)
    {
        var rpcId = OnGenerateRpcId?.Invoke() ?? throw new Exception("OnGenerateRpcId is null");
        var pkg = PackageHelper.FromProtoBuf(message, rpcId);
        var bytes = pkg.ToBytes();

        // ToBytes returns ReadOnlyMemory<byte> but kcp2k needs an
        // ArraySegment - both back onto the same heap array so this is a
        // free reinterpretation when the underlying buffer is array-backed
        // (which PackageHelper.ToBytes guarantees).
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out var segment))
        {
            this.sendImpl(segment);
        }
        else
        {
            // Defensive fallback - copy. Should never hit in practice.
            var copy = bytes.ToArray();
            this.sendImpl(new ArraySegment<byte>(copy));
        }
    }

    /// <inheritdoc/>
    public override void Send(ReadOnlyMemory<byte> bytes)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out var segment))
        {
            this.sendImpl(segment);
        }
        else
        {
            var copy = bytes.ToArray();
            this.sendImpl(new ArraySegment<byte>(copy));
        }
    }
}
