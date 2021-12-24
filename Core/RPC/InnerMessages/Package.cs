using System;

namespace LPS.Core.RPC.InnerMessages
{
    /// <summary>
    /// Package is the unit send and recv inside LPS
    /// The structure of the Package is as follow:
    /// 
    /// -----------------------------------------------------------------------
    /// Header | package_len uint16 | id uint32 | version uint16 | type uint 16
    /// -----------------------------------------------------------------------
    /// Body | Maximum 4kb
    /// -----------------------------------------------------------------------
    /// </summary>
    
    public struct PackageHeader
    {
        UInt16 Length;
        UInt32 ID;
        UInt16 Version;
        UInt16 Type;
    }

    public struct Package
    {
        PackageHeader Header;
        byte[] Body;
    }
}
