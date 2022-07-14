using LPS.Core.Rpc.InnerMessages;

namespace LPS.Client
{
    namespace LPS.Core.Rpc
    {
        public static class RpcProtobufDefs
        {
            private static readonly Dictionary<PackageType, PackageHelper.CreateIMessage> Type2ProBuf_ = new()
            {
                {PackageType.Authentication, (in Package pkg) => PackageHelper.GetProtoBufObject<Authentication>(pkg)},
                {PackageType.EntityRpc, (in Package pkg) => PackageHelper.GetProtoBufObject<EntityRpc>(pkg)},
                {PackageType.ClientCreateEntity, (in Package pkg) => PackageHelper.GetProtoBufObject<ClientCreateEntity>(pkg)},
                {PackageType.RequirePropertyFullSync, (in Package pkg) => PackageHelper.GetProtoBufObject<RequirePropertyFullSync>(pkg)},
                {PackageType.PropertyFullSync, (in Package pkg) => PackageHelper.GetProtoBufObject<PropertyFullSync>(pkg)},
                {PackageType.PropertySync, (in Package pkg) => PackageHelper.GetProtoBufObject<PropertySync>(pkg)},
                {PackageType.PropertySyncAck, (in Package pkg) => PackageHelper.GetProtoBufObject<PropertySyncAck>(pkg)},
                {PackageType.PropertyFullSyncAck, (in Package pkg) => PackageHelper.GetProtoBufObject<PropertyFullSyncAck>(pkg)},
            };

            private static readonly Dictionary<Type, PackageType> Type2Enum_ = new()
            {
                {typeof(Authentication), PackageType.Authentication},
                {typeof(EntityRpc), PackageType.EntityRpc},
                {typeof(ClientCreateEntity), PackageType.ClientCreateEntity},
                {typeof(RequirePropertyFullSync), PackageType.RequirePropertyFullSync},
                {typeof(PropertyFullSync), PackageType.PropertyFullSync},
                {typeof(PropertySync), PackageType.PropertySync},
                {typeof(PropertySyncAck), PackageType.PropertySyncAck},
                {typeof(PropertyFullSyncAck), PackageType.PropertyFullSyncAck},
            };

            public static void Init()
            {
                PackageHelper.SetType2Protobuf(Type2ProBuf_);
                PackageHelper.SetType2Enum(Type2Enum_);
            }
        }
    }
}