// -----------------------------------------------------------------------
// <copyright file="Message.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Ipc;

using MessageType = System.ValueTuple<Google.Protobuf.IMessage, LPS.Common.Rpc.Connection, uint>;

/// <summary>
/// Message.
/// </summary>
public struct Message
{
    /// <summary>
    /// Key of the message.
    /// </summary>
    public readonly IComparable Key;

    /// <summary>
    /// Argument of the message.
    /// </summary>
    public readonly MessageType Arg;

    /// <summary>
    /// Initializes a new instance of the <see cref="Message"/> struct.
    /// </summary>
    /// <param name="key">Key of the message.</param>
    /// <param name="arg">Argument of the message.</param>
    public Message(IComparable key, MessageType arg)
    {
        this.Key = key;
        this.Arg = arg;
    }
}