using System;
using System.Collections.Generic;
using Google.Protobuf;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Server.Core.Rpc.InnerMessages;

namespace LPS.Server.Core.Rpc
{
    public static class RpcProtobufDefs
    {
        private static readonly Dictionary<PackageType, PackageHelper.CreateIMessage> Type2ProBuf_ = new();
        private static readonly Dictionary<Type, PackageType> Type2Enum_ = new();


        private static void RegisterProtobufDef<TP>(PackageType type) where TP : IMessage<TP>, new()
        {
            Type2ProBuf_[type] = (in Package pkg) => PackageHelper.GetProtoBufObject<TP>(pkg);
            Type2Enum_[typeof(TP)] = type;
        }
        
        public static void Initialize()
        {
            #region Protobuf type mapping definition

            RegisterProtobufDef<Authentication>(PackageType.Authentication);
            RegisterProtobufDef<CreateEntity>(PackageType.CreateEntity);
            RegisterProtobufDef<CreateEntityRes>(PackageType.CreateEntityRes);
            RegisterProtobufDef<ExchangeMailBox>(PackageType.ExchangeMailBox);
            RegisterProtobufDef<ExchangeMailBoxRes>(PackageType.ExchangeMailBoxRes);
            RegisterProtobufDef<Control>(PackageType.Control);
            RegisterProtobufDef<EntityRpc>(PackageType.EntityRpc);
            RegisterProtobufDef<ClientCreateEntity>(PackageType.ClientCreateEntity);
            RegisterProtobufDef<RequirePropertyFullSync>(PackageType.RequirePropertyFullSync);
            RegisterProtobufDef<PropertyFullSync>(PackageType.PropertyFullSync);
            RegisterProtobufDef<PropertySync>(PackageType.PropertySync);
            RegisterProtobufDef<PropertySyncAck>(PackageType.PropertySyncAck);
            RegisterProtobufDef<PropertyFullSyncAck>(PackageType.PropertyFullSyncAck);

            #endregion

            PackageHelper.SetType2Protobuf(Type2ProBuf_);
            PackageHelper.SetType2Enum(Type2Enum_);
        }
    }
}