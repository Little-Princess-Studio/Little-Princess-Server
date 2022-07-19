using System;
using System.Collections.Generic;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc
{
    public static class RpcProtobufDefs
    {
        private static readonly Dictionary<PackageType, PackageHelper.CreateIMessage> Type2ProBuf_ = new()
        {
            { PackageType.Authentication, (in Package pkg) => PackageHelper.GetProtoBufObject<Authentication>(pkg) },
            { PackageType.CreateEntity, (in Package pkg) => PackageHelper.GetProtoBufObject<CreateEntity>(pkg) },
            { PackageType.CreateEntityRes, (in Package pkg) => PackageHelper.GetProtoBufObject<CreateEntityRes>(pkg) },
            { PackageType.ExchangeMailBox, (in Package pkg) => PackageHelper.GetProtoBufObject<ExchangeMailBox>(pkg) },
            { PackageType.ExchangeMailBoxRes, (in Package pkg) => PackageHelper.GetProtoBufObject<ExchangeMailBoxRes>(pkg) },
            { PackageType.Control, (in Package pkg) => PackageHelper.GetProtoBufObject<Control>(pkg) },
            { PackageType.EntityRpc, (in Package pkg) => PackageHelper.GetProtoBufObject<EntityRpc>(pkg) },
            { PackageType.RequirePropertyFullSync, (in Package pkg) => PackageHelper.GetProtoBufObject<RequirePropertyFullSync>(pkg) },
            { PackageType.PropertyFullSync, (in Package pkg) => PackageHelper.GetProtoBufObject<PropertyFullSync>(pkg) },
            { PackageType.PropertySync, (in Package pkg) => PackageHelper.GetProtoBufObject<PropertySync>(pkg) },
        };

        private static readonly Dictionary<Type, PackageType> Type2Enum_ = new()
        {
            { typeof(Authentication), PackageType.Authentication },
            { typeof(CreateEntity), PackageType.CreateEntity },
            { typeof(CreateEntityRes), PackageType.CreateEntityRes },
            { typeof(ExchangeMailBox), PackageType.ExchangeMailBox },
            { typeof(ExchangeMailBoxRes), PackageType.ExchangeMailBoxRes },
            { typeof(Control), PackageType.Control },
            { typeof(EntityRpc), PackageType.EntityRpc },
            { typeof(ClientCreateEntity), PackageType.ClientCreateEntity },
            { typeof(RequirePropertyFullSync), PackageType.RequirePropertyFullSync },
            { typeof(PropertyFullSync), PackageType.PropertyFullSync },
            { typeof(PropertySync), PackageType.PropertySync },
        };

        public static void Initialize()
        {
            PackageHelper.SetType2Protobuf(Type2ProBuf_);
            PackageHelper.SetType2Enum(Type2Enum_);
        }
    }
}