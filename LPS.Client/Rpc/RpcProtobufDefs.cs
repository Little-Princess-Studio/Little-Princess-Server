// -----------------------------------------------------------------------
// <copyright file="RpcProtobufDefs.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc;

using Google.Protobuf;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Util;

/// <summary>
/// Rpc protobuf related definitions, used to initialize the Rpc type registration.
/// </summary>
public static class RpcProtobufDefs
{
    private static readonly Dictionary<PackageType, PackageHelper.CreateIMessage> Type2ProBuf = new();
    private static readonly Dictionary<Type, PackageType> Type2Enum = new(TypeExtensions.GetTypeEqualityComparer());

    static RpcProtobufDefs()
    {
    }

    /// <summary>
    /// Initialize the protobuf related type registration.
    /// </summary>
    public static void Init()
    {
        #region Protobuf type mapping definition

        RegisterProtobufDef<Authentication>(PackageType.Authentication);
        RegisterProtobufDef<EntityRpc>(PackageType.EntityRpc);
        RegisterProtobufDef<ClientCreateEntity>(PackageType.ClientCreateEntity);
        RegisterProtobufDef<RequirePropertyFullSync>(PackageType.RequirePropertyFullSync);
        RegisterProtobufDef<PropertyFullSync>(PackageType.PropertyFullSync);
        RegisterProtobufDef<PropertySyncCommandList>(PackageType.PropertySyncCommandList);
        RegisterProtobufDef<RequireComponentSync>(type: PackageType.RequireComponentSync);
        RegisterProtobufDef<ComponentSync>(PackageType.ComponentSync);

        #endregion

        PackageHelper.SetType2Protobuf(Type2ProBuf);
        PackageHelper.SetType2Enum(Type2Enum);
    }

    private static void RegisterProtobufDef<TP>(PackageType type)
        where TP : IMessage<TP>, new()
    {
        Type2ProBuf[type] = (in Package pkg) => PackageHelper.GetProtoBufObject<TP>(pkg);
        Type2Enum[typeof(TP)] = type;
    }
}