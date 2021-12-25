using System;
using System.Runtime.InteropServices;

namespace LPS.Core.RPC.InnerMessages
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
    
    [StructLayout(LayoutKind.Explicit, Size = 8)]
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
        public PackageHeader Header = default;
        public byte[] Body = null;
    }

    public static class PackageHelper
    {
        public static Package MakePackage<T>(T protoBufObj)
        {
            return default;
        }

        public static Package GetPackageFromRaw<T>(ArraySegment<byte> rawData)
        {
            return default;
        }

        public static T GetProtoBufObject<T>(Package package)
        {
            return default;
        }
    }
}
