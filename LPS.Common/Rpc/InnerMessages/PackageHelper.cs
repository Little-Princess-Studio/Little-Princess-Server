// -----------------------------------------------------------------------
// <copyright file="PackageHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.InnerMessages;

using Google.Protobuf;

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

    private static Dictionary<PackageType, CreateIMessage> type2Protobuf = null!;
    private static Dictionary<Type, PackageType> type2Enum = null!;

    /// <summary>
    /// Set the mapping dict of PackageType -> Message Create Handler.
    /// </summary>
    /// <param name="type2Protobuf">Dict of PackageType -> Message Create Handler.</param>
    public static void SetType2Protobuf(Dictionary<PackageType, CreateIMessage> type2Protobuf)
    {
        PackageHelper.type2Protobuf = type2Protobuf;
    }

    /// <summary>
    /// Set the mapping dict of Package type -> Package enum type.
    /// </summary>
    /// <param name="type2Enum">Dict of Package type -> Package enum type.</param>
    public static void SetType2Enum(Dictionary<Type, PackageType> type2Enum)
    {
        PackageHelper.type2Enum = type2Enum;
    }

    private static class MessageParserWrapper<T>
        where T : IMessage<T>, new()
    {
        private static readonly MessageParser<T> Parser = new(() => new T());

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
        var pkg = default(Package);
        var bytes = protobufObj.ToByteArray();

        pkg.Header.Length = (ushort)(PackageHeader.Size + bytes.Length);
        pkg.Header.Version = 0x0001;
        pkg.Header.ID = id;
        pkg.Header.Type = (ushort)PackageHelper.GetPackageType<T>();
        pkg.Body = bytes;

        return pkg;
    }

    /// <summary>
    /// Generate package from protobuf object.
    /// </summary>
    /// <param name="msg">Protobuf object.</param>
    /// <param name="id">Package id.</param>
    /// <returns>Package.</returns>
    public static Package FromProtoBuf(IMessage msg, uint id)
    {
        var pkg = default(Package);
        var bytes = msg.ToByteArray();

        pkg.Header.Length = (ushort)(PackageHeader.Size + bytes.Length);
        pkg.Header.Version = 0x0001;
        pkg.Header.ID = id;
        pkg.Header.Type = (ushort)GetPackageType(msg.GetType());
        pkg.Body = bytes;

        return pkg;
    }

    private static PackageType GetPackageType<T>() => type2Enum[typeof(T)];

    private static PackageType GetPackageType(Type type) => type2Enum[type];
}