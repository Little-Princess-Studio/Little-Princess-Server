// -----------------------------------------------------------------------
// <copyright file="MailBox.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

/// <summary>
/// MailBox class. MailBox is a fundamental component of every entity on the server
/// which is unique within server-wide.
/// </summary>
public readonly struct MailBox
{
    /// <summary>
    /// Id of the MailBox.
    /// </summary>
    public readonly string Id;

    /// <summary>
    /// Ip of the MailBox.
    /// </summary>
    public readonly string Ip;

    /// <summary>
    /// Port of the MailBox.
    /// </summary>
    public readonly int Port;

    /// <summary>
    /// HostNum of the MailBox.
    /// </summary>
    public readonly int HostNum;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailBox"/> struct.
    /// </summary>
    /// <param name="id">Id of the MailBox.</param>
    /// <param name="ip">Ip of the MailBox.</param>
    /// <param name="port">Port of the MailBox.</param>
    /// <param name="hostNum">HostNum of the MailBox.</param>
    public MailBox(string id, string ip, int port, int hostNum)
    {
        this.Id = id;
        this.Ip = ip;
        this.Port = port;
        this.HostNum = hostNum;
    }

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        return $"{this.Id} {this.Ip} {this.Port} {this.HostNum}";
    }

    /// <summary>
    /// Compare with another MailBox only by Id.
    /// </summary>
    /// <param name="other">Target MailBox to compare with.</param>
    /// <returns>If this MailBox equals to the other MailBox.</returns>
    public readonly bool CompareOnlyID(MailBox other)
    {
        return this.Id == other.Id;
    }

    /// <summary>
    /// Compare with another MailBox only by Id.
    /// </summary>
    /// <param name="other">Target MailBox to compare with.</param>
    /// <returns>If this MailBox equals to the other MailBox.</returns>
    public readonly bool CompareOnlyID(InnerMessages.MailBox other)
    {
        return this.Id == other.ID;
    }

    /// <summary>
    /// Compare with another MailBox.
    /// </summary>
    /// <param name="other">Target MailBox to compare with.</param>
    /// <returns>If this MailBox equals to the other MailBox.</returns>
    public readonly bool CompareFull(MailBox other)
    {
        return this.Id == other.Id
               && this.Ip == other.Ip
               && this.Port == other.Port
               && this.HostNum == other.HostNum;
    }

    /// <summary>
    /// Compare with another MailBox.
    /// </summary>
    /// <param name="other">Target MailBox to compare with.</param>
    /// <returns>If this MailBox equals to the other MailBox.</returns>
    public readonly bool CompareFull(InnerMessages.MailBox other)
    {
        return this.Id == other.ID
               && this.Ip == other.IP
               && this.Port == other.Port
               && this.HostNum == other.HostNum;
    }

    /// <summary>
    /// Compare with another MailBox only by Ip:Port.
    /// </summary>
    /// <param name="other">Target MailBox to compare with.</param>
    /// <returns>If this MailBox equals to the other MailBox.</returns>
    public readonly bool CompareOnlyAddress(InnerMessages.MailBox other)
    {
        return this.Ip == other.IP && this.Port == other.Port;
    }

    /// <summary>
    /// Compare with another MailBox only by Ip:Port.
    /// </summary>
    /// <param name="other">Target MailBox to compare with.</param>
    /// <returns>If this MailBox equals to the other MailBox.</returns>
    public readonly bool CompareOnlyAddress(MailBox other)
    {
        return this.Ip == other.Ip && this.Port == other.Port;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        if (obj is MailBox other)
        {
            return this.CompareFull(other);
        }

        return false;
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static bool operator ==(MailBox left, MailBox right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MailBox left, MailBox right)
    {
        return !(left == right);
    }
}