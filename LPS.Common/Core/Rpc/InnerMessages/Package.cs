using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Google.Protobuf;

namespace LPS.Core.Rpc.InnerMessages
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
    }

    public static class PackageHelper
    {
        public delegate IMessage CreateIMessage(in Package pkg);
        
        private static readonly Dictionary<PackageType, CreateIMessage> Type2ProBuf = new()
        {
            { PackageType.Authentication, (in Package pkg) => GetProtoBufObject<Authentication>(pkg) },
#if SERVER_SIDE
            { PackageType.CreateEntity, (in Package pkg) => GetProtoBufObject<CreateEntity>(pkg) },
            { PackageType.CreateEntityRes, (in Package pkg) => GetProtoBufObject<CreateEntityRes>(pkg) },
            { PackageType.ExchangeMailBox, (in Package pkg) => GetProtoBufObject<ExchangeMailBox>(pkg) },
            { PackageType.ExchangeMailBoxRes, (in Package pkg) => GetProtoBufObject<ExchangeMailBoxRes>(pkg) },
            { PackageType.Control, (in Package pkg) => GetProtoBufObject<Control>(pkg) },
#endif
            { PackageType.EntityRpc, (in Package pkg) => GetProtoBufObject<EntityRpc>(pkg) },
        };
        
        private static Dictionary<Type, PackageType> Type2Enum = new()
        {
            { typeof(Authentication), PackageType.Authentication },
#if SERVER_SIDE            
            { typeof(CreateEntity), PackageType.CreateEntity },
            { typeof(CreateEntityRes), PackageType.CreateEntityRes },
            { typeof(ExchangeMailBox), PackageType.ExchangeMailBox },
            { typeof(ExchangeMailBoxRes), PackageType.ExchangeMailBoxRes },
            { typeof(Control), PackageType.Control },
#endif
            { typeof(EntityRpc), PackageType.EntityRpc },
        };
        
        private static class MessageParserWrapper<T> where T : IMessage<T>, new ()
        {
            private static readonly MessageParser<T> Parser = new(() => new T());

            public static MessageParser<T> Get() => Parser;
        }

        private static T GetProtoBufObject<T>(in Package package) where T : IMessage<T>, new ()
        {
            var parser = MessageParserWrapper<T>.Get();
            return parser.ParseFrom(package.Body);
        }

        public static IMessage GetProtoBufObjectByType(PackageType type, in Package package)
        {
            return Type2ProBuf[type].Invoke(package);
        }

        private static PackageType GetPackageType<T>() => Type2Enum[typeof(T)];

        private static PackageType GetPackageType(Type type) => Type2Enum[type];

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