// -----------------------------------------------------------------------
// <copyright file="PackageHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.InnerMessages;

using System.Collections.ObjectModel;
using Google.Protobuf;
using LPS.Common.Util;

/// <summary>
/// Package helper class.
/// </summary>
public static class PackageHelper
{
    /// <summary>
    /// Handler to create a protobuf message from a parsed package.
    /// </summary>
    /// <param name="pkg">Package.</param>
    /// <returns>Created protobuf message.</returns>
    public delegate IMessage CreateIMessage(in Package pkg);

    private static ReadOnlyDictionary<PackageType, CreateIMessage> type2Protobuf = null!;
    private static ReadOnlyDictionary<Type, PackageType> type2Enum = null!;

    /// <summary>
    /// Set the mapping dict of PackageType -> Message Create Handler.
    /// </summary>
    /// <param name="type2Protobuf">Dict of PackageType -> Message Create Handler.</param>
    public static void SetType2Protobuf(Dictionary<PackageType, CreateIMessage> type2Protobuf)
    {
        PackageHelper.type2Protobuf = new(type2Protobuf);
    }

    /// <summary>
    /// Set the mapping dict of Package type -> Package enum type.
    /// </summary>
    /// <param name="type2Enum">Dict of Package type -> Package enum type.</param>
    public static void SetType2Enum(Dictionary<Type, PackageType> type2Enum)
    {
        PackageHelper.type2Enum = new(type2Enum);
    }

    /// <summary>
    /// A wrapper class for the Google.Protobuf.MessageParser class that provides a static method to get the parser for a given protobuf message type.
    /// </summary>
    /// <typeparam name="T">The type of the protobuf message.</typeparam>
    public static class MessageParserWrapper<T>
        where T : IMessage<T>, new()
    {
        /// <summary>
        /// The parser for the protobuf message type T.
        /// </summary>
        private static readonly MessageParser<T> Parser = new(() => new T());

        /// <summary>
        /// Gets the parser for the protobuf message type T.
        /// </summary>
        /// <returns>The parser for the protobuf message type T.</returns>
        public static MessageParser<T> Get() => Parser;
    }

    /// <summary>
    /// Get protobuf message object from package.
    /// </summary>
    /// <param name="package">Package object.</param>
    /// <typeparam name="T">Type of protobuf message.</typeparam>
    /// <returns>Protobuf message.</returns>
    public static T GetProtoBufObject<T>(in Package package)
        where T : IMessage<T>, new()
    {
        var parser = MessageParserWrapper<T>.Get();
        return parser.ParseFrom(package.Body);
    }

    /// <summary>
    /// Get protobuf message object from package.
    /// </summary>
    /// <param name="type">Package enum type.</param>
    /// <param name="package">Parsed package.</param>
    /// <returns>Protobuf message.</returns>
    public static IMessage GetProtoBufObjectByType(PackageType type, in Package package)
    {
        return type2Protobuf[type].Invoke(package);
    }

    /// <summary>
    /// Get package from protobuf.
    /// </summary>
    /// <param name="protobufObj">Protobuf object.</param>
    /// <param name="id">Package id.</param>
    /// <typeparam name="T">Type of protobuf object.</typeparam>
    /// <returns>Package.</returns>
    public static Package FromProtoBuf<T>(T protobufObj, uint id)
        where T : IMessage<T>, new()
    {
        var bytes = protobufObj.ToByteArray();

        return new Package(
            new PackageHeader(
                length: (ushort)(PackageHeader.Size + bytes.Length),
                id: id,
                version: 0x0001,
                type: (ushort)PackageHelper.GetPackageType<T>()),
            bytes);
    }

    /// <summary>
    /// Generate package from protobuf object.
    /// </summary>
    /// <param name="msg">Protobuf object.</param>
    /// <param name="id">Package id.</param>
    /// <returns>Package.</returns>
    public static Package FromProtoBuf(IMessage msg, uint id)
    {
        var bytes = msg.ToByteArray();

        return new Package(
            new PackageHeader(
                length: (ushort)(PackageHeader.Size + bytes.Length),
                id: id,
                version: 0x0001,
                type: (ushort)PackageHelper.GetPackageType(msg.GetType())),
            bytes);
    }

    /// <summary>
    /// Get package type from protobuf message type.
    /// </summary>
    /// <typeparam name="T">Protobuf package type.</typeparam>
    /// <returns><see cref="PackageType"/>.</returns>
    public static PackageType GetPackageType<T>() => type2Enum[typeof(T)];

    /// <summary>
    /// Get package type from protobuf message type.
    /// </summary>
    /// <param name="type">Protobuf package type.</param>
    /// <returns><see cref="PackageType"/>.</returns>
    public static PackageType GetPackageType(Type type) => type2Enum[type];
}