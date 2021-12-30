using System;
using System.Collections.Generic;
using System.Net;
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

        public PackageHeader(ushort length, uint id, ushort version, ushort type)
        {
            Length = length;
            ID = id;
            Version = version;
            Type = type;
        }
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
        CreateEntityRes,
        EntityRpc,
        CreateEntity,
        ExchangeMailBox,
        ExchangeMailBoxRes,
    }

    public static class PackageHelper
    {
        private delegate IMessage CreateIMessage(in Package pkg);
        private static readonly Dictionary<PackageType, CreateIMessage> Type2ProBuf = new()
        {
            { PackageType.Authentication, (in Package pkg) => GetProtoBufObject<Authentication>(pkg) },
            { PackageType.CreateEntity, (in Package pkg) => GetProtoBufObject<CreateEntity>(pkg) },
            { PackageType.CreateEntityRes, (in Package pkg) => GetProtoBufObject<CreateEntityRes>(pkg) },
            { PackageType.ExchangeMailBox, (in Package pkg) => GetProtoBufObject<ExchangeMailBox>(pkg) },
            { PackageType.ExchangeMailBoxRes, (in Package pkg) => GetProtoBufObject<ExchangeMailBoxRes>(pkg) },
        };

        private static readonly Dictionary<Type, PackageType> Type2Enum = new()
        {
            { typeof(Authentication), PackageType.Authentication },
            { typeof(CreateEntity), PackageType.CreateEntity },
            { typeof(CreateEntityRes), PackageType.CreateEntityRes },
            { typeof(ExchangeMailBox), PackageType.ExchangeMailBox },
            { typeof(ExchangeMailBoxRes), PackageType.ExchangeMailBoxRes },
        };

        private static class MessageParserWrapper<T> where T : IMessage<T>, new ()
        {
            private static readonly MessageParser<T> parser_ = new(() => new T());

            public static MessageParser<T> Get() => parser_;
        }

        public static T GetProtoBufObject<T>(in Package package) where T : IMessage<T>, new ()
        {
            var parser = MessageParserWrapper<T>.Get();
            return parser.ParseFrom(package.Body);
        }

        public static IMessage GetProtoBufObjectByType(PackageType type, in Package package)
        {
            return Type2ProBuf[type].Invoke(package);
        }

        public static PackageType GetPackageType<T>() => Type2Enum[typeof(T)];

        public static PackageType GetPackageType(Type type) => Type2Enum[type];

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
