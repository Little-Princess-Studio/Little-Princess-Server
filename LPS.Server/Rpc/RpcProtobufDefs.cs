// -----------------------------------------------------------------------
// <copyright file="RpcProtobufDefs.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using System;
using System.Collections.Generic;
using Google.Protobuf;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.InnerMessages.ProtobufDefs;
using LPS.Server.Rpc.InnerMessages.ProtobufDefs;

/// <summary>
/// Rpc protobuf related definitions, used to initialize the Rpc type registration.
/// </summary>
public static class RpcProtobufDefs
{
    private static readonly Dictionary<PackageType, PackageHelper.CreateIMessage> Type2ProBuf = new();
    private static readonly Dictionary<Type, PackageType> Type2Enum = new();

    /// <summary>
    /// Initialize the protobuf related type registration.
    /// </summary>
    public static void Initialize()
    {
        #region Protobuf type mapping definition

        RegisterProtobufDef<Authentication>(PackageType.Authentication);
        RegisterProtobufDef<RequireCreateEntity>(PackageType.RequireCreateEntity);
        RegisterProtobufDef<RequireCreateEntityRes>(PackageType.RequireCreateEntityRes);
        RegisterProtobufDef<ExchangeMailBox>(PackageType.ExchangeMailBox);
        RegisterProtobufDef<ExchangeMailBoxRes>(PackageType.ExchangeMailBoxRes);
        RegisterProtobufDef<Control>(PackageType.Control);
        RegisterProtobufDef<EntityRpc>(PackageType.EntityRpc);
        RegisterProtobufDef<ClientCreateEntity>(PackageType.ClientCreateEntity);
        RegisterProtobufDef<RequirePropertyFullSync>(PackageType.RequirePropertyFullSync);
        RegisterProtobufDef<PropertyFullSync>(PackageType.PropertyFullSync);
        RegisterProtobufDef<PropertySyncCommandList>(PackageType.PropertySyncCommandList);
        RegisterProtobufDef<PropertySyncAck>(PackageType.PropertySyncAck);
        RegisterProtobufDef<PropertyFullSyncAck>(PackageType.PropertyFullSyncAck);
        RegisterProtobufDef<HostCommand>(PackageType.HostCommand);
        RegisterProtobufDef<CreateDistributeEntity>(PackageType.CreateDistributeEntity);
        RegisterProtobufDef<CreateDistributeEntityRes>(PackageType.CreateDistributeEntityRes);

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