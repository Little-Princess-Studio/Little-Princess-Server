using System.Runtime.InteropServices;
using Google.Protobuf;

namespace LPS.Common.Core.Rpc.InnerMessages
{
    /// <summary>
    /// Package is the unit send and recv inside LPS
    /// The structure of the Package is as follow:
    /// 
    /// -----------------------------------------------------------------------
    /// Header | package_len uint16 | id uint32 | version uint16 | type uint16
    /// -----------------------------------------------------------------------
    /// Body | Maximum 4kb
    /// -----------------------------------------------------------------------
    /// </summary>
    
    [StructLayout(LayoutKind.Explicit, Size = 10)]
    public struct PackageHeader
    {
        [FieldOffset(0)]
        public UInt16 Length;
        [FieldOffset(2)]
        public UInt32 ID;
        [FieldOffset(6)]
        public UInt16 Version;
        [FieldOffset(8)]
        public UInt16 Type;

        public static readonly int Size = Marshal.SizeOf<PackageHeader>();
    }

    public struct Package
    {
        public PackageHeader Header;
        public byte[] Body;
       
        public byte[] ToBytes()
        {
            byte[] bytes = new byte[this.Header.Length];

            var tmpBytes = BitConverter.GetBytes(this.Header.Length);
            int pos = 0;
            Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
            pos += tmpBytes.Length;
            
            tmpBytes = BitConverter.GetBytes(this.Header.ID);
            Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
            pos += tmpBytes.Length;
            
            tmpBytes = BitConverter.GetBytes(this.Header.Version);
            Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
            pos += tmpBytes.Length;
            
            tmpBytes = BitConverter.GetBytes(this.Header.Type);
            Buffer.BlockCopy(tmpBytes, 0, bytes, pos, tmpBytes.Length);
            pos += tmpBytes.Length;
            
            Buffer.BlockCopy(Body, 0, bytes, pos, Body.Length);

            return bytes;
        }
        
        public override string ToString()
        {
            return $"{Header.Length} {Header.ID} {Header.Version} {Header.Type}";
        }
    }

    public enum PackageType
    {
        Authentication = 0,
        CreateEntityRes = 1,
        EntityRpc = 2,
        CreateEntity = 3,
        ExchangeMailBox = 4,
        ExchangeMailBoxRes = 5,
        Control = 6,
        ClientCreateEntity = 7,
        RequirePropertyFullSync = 8,
        PropertyFullSync = 9,
        PropertySyncCommandList = 10,
        PropertyFullSyncAck = 11,
        PropertySyncAck = 12,
    }

    public static class PackageHelper
    {
        public delegate IMessage CreateIMessage(in Package pkg);
        
        private static Dictionary<PackageType, CreateIMessage> Type2Protobuf_ = null!;
        private static Dictionary<Type, PackageType> Type2Enum_ = null!;

        public static void SetType2Protobuf(Dictionary<PackageType, CreateIMessage> type2Protobuf)
        {
            Type2Protobuf_ = type2Protobuf;
        }

        public static void SetType2Enum(Dictionary<Type, PackageType> type2Enum)
        {
            Type2Enum_ = type2Enum;
        }

        private static class MessageParserWrapper<T> where T : IMessage<T>, new ()
        {
            private static readonly MessageParser<T> Parser_ = new(() => new T());

            public static MessageParser<T> Get() => Parser_;
        }

        public static T GetProtoBufObject<T>(in Package package) where T : IMessage<T>, new ()
        {
            var parser = MessageParserWrapper<T>.Get();
            return parser.ParseFrom(package.Body);
        }

        public static IMessage GetProtoBufObjectByType(PackageType type, in Package package)
        {
            return Type2Protobuf_[type].Invoke(package);
        }

        private static PackageType GetPackageType<T>() => Type2Enum_[typeof(T)];

        private static PackageType GetPackageType(Type type) => Type2Enum_[type];

        public static Package FromProtoBuf<T>(T protobufObj, uint id) where T : IMessage<T>, new ()
        {
            var pkg = new Package();
            var bytes = protobufObj.ToByteArray();

            pkg.Header.Length = (UInt16)(PackageHeader.Size + bytes.Length);
            pkg.Header.Version = 0x0001;
            pkg.Header.ID = id;
            pkg.Header.Type = (UInt16)PackageHelper.GetPackageType<T>();
            pkg.Body = bytes;

            return pkg;
        }

        public static Package FromProtoBuf(IMessage msg, uint id)
        {
            var pkg = new Package();
            var bytes = msg.ToByteArray();

            pkg.Header.Length = (UInt16)(PackageHeader.Size + bytes.Length);
            pkg.Header.Version = 0x0001;
            pkg.Header.ID = id;
            pkg.Header.Type = (UInt16)GetPackageType(msg.GetType());
            pkg.Body = bytes;

            return pkg;
        }
    }
}
