// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.InnerMessages;

using System.Runtime.InteropServices;

/// <summary>
/// Represents a network package.
/// </summary>
public readonly struct Package
{
    /// <summary>
    /// Gets the package header.
    /// </summary>
    public readonly PackageHeader Header;

    /// <summary>
    /// Gets the package body.
    /// </summary>
    public readonly Memory<byte> Body;

    /// <summary>
    /// Initializes a new instance of the <see cref="Package"/> struct.
    /// </summary>
    /// <param name="header">The package header.</param>
    /// <param name="body">The package body.</param>
    public Package(in PackageHeader header, Memory<byte> body)
    {
        this.Header = header;
        this.Body = body;
    }

    /// <summary>
    /// Convert package object to bytes.
    /// </summary>
    /// <returns>Byte array.</returns>
    public ReadOnlyMemory<byte> ToBytes()
    {
        Memory<byte> bytes = new byte[this.Header.Length];

        int pos = 0;
        ReadOnlySpan<byte> tmpBytes = BitConverter.GetBytes(this.Header.Length);
        tmpBytes.CopyTo(bytes.Span[pos..]);

        // Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        tmpBytes = BitConverter.GetBytes(this.Header.ID);
        tmpBytes.CopyTo(bytes.Span[pos..]);

        // Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        tmpBytes = BitConverter.GetBytes(this.Header.Version);
        tmpBytes.CopyTo(bytes.Span[pos..]);

        // Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        tmpBytes = BitConverter.GetBytes(this.Header.Type);
        tmpBytes.CopyTo(bytes.Span[pos..]);

        // Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        // Buffer.BlockCopy(this.Body, 0, bytes, pos, this.Body.Length);
        this.Body.Span.CopyTo(bytes.Span[pos..]);

        return bytes;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Header.Length} {this.Header.ID} {this.Header.Version} {this.Header.Type}";
    }
}

#pragma warning disable SA1629

/// <summary>
/// <para>
/// Package is the unit send and recv inside LPS
/// The structure of the Package is as follow:
/// </para>
/// <para>
/// -----------------------------------------------------------------------
/// Header | package_len uint16 | id uint32 | version uint16 | type uint16
/// -----------------------------------------------------------------------
/// Body | Maximum 4kb
/// -----------------------------------------------------------------------
/// </para>
/// Represents the header of a network package.
/// </summary>
#pragma warning restore SA1629
[StructLayout(LayoutKind.Explicit, Size = 10)]
public readonly struct PackageHeader
{
    /// <summary>
    /// The size of the package header.
    /// </summary>
    public static readonly int Size = Marshal.SizeOf<PackageHeader>();

    /// <summary>
    /// The length of the package.
    /// </summary>
    [FieldOffset(0)]
    public readonly ushort Length;

    /// <summary>
    /// The ID of the package.
    /// </summary>
    [FieldOffset(2)]
    public readonly uint ID;

    /// <summary>
    /// The version of the package.
    /// </summary>
    [FieldOffset(6)]
    public readonly ushort Version;

    /// <summary>
    /// The type of the package.
    /// </summary>
    [FieldOffset(8)]
    public readonly ushort Type;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageHeader"/> struct with the specified values.
    /// </summary>
    /// <param name="length">The length of the package.</param>
    /// <param name="id">The ID of the package.</param>
    /// <param name="version">The version of the package.</param>
    /// <param name="type">The type of the package.</param>
    public PackageHeader(ushort length, uint id, ushort version, ushort type)
    {
        this.Length = length;
        this.ID = id;
        this.Version = version;
        this.Type = type;
    }
}

/// <summary>
/// Package type.
/// </summary>
public enum PackageType
{
#pragma warning disable SA1602
    Authentication = 0,
    RequireCreateEntityRes = 1,
    EntityRpc = 2,
    RequireCreateEntity = 3,
    ExchangeMailBox = 4,
    ExchangeMailBoxRes = 5,
    Control = 6,
    ClientCreateEntity = 7,
    RequirePropertyFullSync = 8,
    PropertyFullSync = 9,
    PropertySyncCommandList = 10,
    HostCommand = 11,
    CreateDistributeEntity = 12,
    CreateDistributeEntityRes = 13,
    RequireComponentSync = 14,
    ComponentSync = 15,
    ServiceRpc = 16,
    ServiceRpcCallBack = 17,
    ServiceControl = 18,
    ServiceManagerCommand = 19,
    EntityRpcCallBack = 20,
    Ping = 21,
    Pong = 22,
#pragma warning restore SA1602
}