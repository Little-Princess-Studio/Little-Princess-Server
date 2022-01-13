using System;
using System.Collections.Generic;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc
{
    public static class RpcProtobufDefs
    {
        private static readonly Dictionary<PackageType, PackageHelper.CreateIMessage> Type2ProBuf = new()
        {
            { PackageType.Authentication, (in Package pkg) => PackageHelper.GetProtoBufObject<Authentication>(pkg) },
            { PackageType.CreateEntity, (in Package pkg) => PackageHelper.GetProtoBufObject<CreateEntity>(pkg) },
            { PackageType.CreateEntityRes, (in Package pkg) => PackageHelper.GetProtoBufObject<CreateEntityRes>(pkg) },
            { PackageType.ExchangeMailBox, (in Package pkg) => PackageHelper.GetProtoBufObject<ExchangeMailBox>(pkg) },
            { PackageType.ExchangeMailBoxRes, (in Package pkg) => PackageHelper.GetProtoBufObject<ExchangeMailBoxRes>(pkg) },
            { PackageType.Control, (in Package pkg) => PackageHelper.GetProtoBufObject<Control>(pkg) },
            { PackageType.EntityRpc, (in Package pkg) => PackageHelper.GetProtoBufObject<EntityRpc>(pkg) },
        };

        private static readonly Dictionary<Type, PackageType> Type2Enum = new()
        {
            { typeof(Authentication), PackageType.Authentication },
            { typeof(CreateEntity), PackageType.CreateEntity },
            { typeof(CreateEntityRes), PackageType.CreateEntityRes },
            { typeof(ExchangeMailBox), PackageType.ExchangeMailBox },
            { typeof(ExchangeMailBoxRes), PackageType.ExchangeMailBoxRes },
            { typeof(Control), PackageType.Control },
            { typeof(EntityRpc), PackageType.EntityRpc },
        };

        public static void Initialize()
        {
            PackageHelper.SetType2Protbuf(Type2ProBuf);
            PackageHelper.SetType2Enum(Type2Enum);
        }
        
    }
}