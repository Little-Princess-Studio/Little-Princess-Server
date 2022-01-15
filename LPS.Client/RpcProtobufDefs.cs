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
            };

            private static readonly Dictionary<Type, PackageType> Type2Enum_ = new()
            {
                {typeof(Authentication), PackageType.Authentication},
                {typeof(EntityRpc), PackageType.EntityRpc},
            };

            public static void Init()
            {
                PackageHelper.SetType2Protobuf(Type2ProBuf_);
                PackageHelper.SetType2Enum(Type2Enum_);
            }
        }
    }
}
