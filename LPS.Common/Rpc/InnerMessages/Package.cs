// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.InnerMessages;

using System.Runtime.InteropServices;

/// <summary>
/// Network package.
/// </summary>
public struct Package
{
    /// <summary>
    /// Header.
    /// </summary>
    public PackageHeader Header;

    /// <summary>
    /// Body.
    /// </summary>
    public byte[] Body;

    /// <summary>
    /// Convert package object to bytes.
    /// </summary>
    /// <returns>Byte array.</returns>
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[this.Header.Length];

        var tmpBytes = BitConverter.GetBytes(this.Header.Length);
        int pos = 0;
        Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        tmpBytes = BitConverter.GetBytes(this.Header.ID);
        Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        tmpBytes = BitConverter.GetBytes(this.Header.Version);
        Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        tmpBytes = BitConverter.GetBytes(this.Header.Type);
        Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
        pos += tmpBytes.Length;

        Buffer.BlockCopy(this.Body, 0, bytes, pos, this.Body.Length);

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
/// Package is the unit send and recv inside LPS
/// The structure of the Package is as follow:
///
/// -----------------------------------------------------------------------
/// Header | package_len uint16 | id uint32 | version uint16 | type uint16
/// -----------------------------------------------------------------------
/// Body | Maximum 4kb
/// -----------------------------------------------------------------------
/// </summary>
#pragma warning restore SA1629
[StructLayout(LayoutKind.Explicit, Size = 10)]
public struct PackageHeader
{
    /// <summary>
    /// Size of the package header.
    /// </summary>
    public static readonly int Size = Marshal.SizeOf<PackageHeader>();

    /// <summary>
    /// Length of the package.
    /// </summary>
    [FieldOffset(0)]
    public ushort Length;

    /// <summary>
    /// Id of the package.
    /// </summary>
    [FieldOffset(2)]
    public uint ID;

    /// <summary>
    /// Version of the package.
    /// </summary>
    [FieldOffset(6)]
    public ushort Version;

    /// <summary>
    /// Package type.
    /// </summary>
    [FieldOffset(8)]
    public ushort Type;
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
    PropertyFullSyncAck = 11,
    PropertySyncAck = 12,
    HostCommand = 13,
    CreateDistributeEntity = 14,
    CreateDistributeEntityRes = 15,
#pragma warning restore SA1602
}