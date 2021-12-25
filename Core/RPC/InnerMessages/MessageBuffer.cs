using System;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Debug;

namespace LPS.Core.RPC.InnerMessages
{
    public class MessageBuffer
    {
        // initial 4k buffer to recv network messages
        const int MaxLength = 4096;
        const int HeaderLen = 8;

        private int head_ = 0;
        private int tail_ = 0;

        private int BodyLen=> tail_ - head_;

        private int curBufLen = MaxLength;

        private byte[] buffer_ = new byte[MaxLength];

        public bool TryRecieveFromRaw(byte[] incomeBuffer, int len, out Package pkg)
        {
            /*
            How to handler TCP stream raw data to Package:

            1. if tail_+len-1 >= current len, double current buf, copy buf[head..tail] to new[0..bodylen], buf = new, tail_ = tail_ - head_, head_ = 0

            2. copy incomeBuffer[0..len] to buf[tail_..tail+len] tail_ = tail_+len

            3. if bodylen < header, do nothing, wait next data
                else
                    header = pick_header_from_raw_data(buf[head..tail])
                    if bodylen == header.package_len then get package from buf[head..tail], reset head = tail = 0
                    if bodylen < header.package_len then wait next data
                    if bodylen > header.package_len then get package from buf[head..head+header.package_len], set head = head + header.package_len
            */

            if (tail_+len-1>= curBufLen)
            {
                while (tail_+len-1>= curBufLen)
                {
                    // repeat double size
                    curBufLen <<= 1;
                    Logger.Debug($"{tail_} + {len} - 1 = {tail_ + len - 1} >= {curBufLen}, double the buf");
                }

                byte[] newBuffer = new byte[curBufLen];

                // copy current data buf[head...tail] -> new[0...tail-head]
                Buffer.BlockCopy(buffer_, head_, newBuffer, 0, BodyLen);
                buffer_ = newBuffer;

                tail_ = BodyLen;
                head_ = 0;
            }

            // copy incomeBuffer to buf [tail_..tail_+len]
            Buffer.BlockCopy(incomeBuffer, 0, buffer_, tail_, len);
            tail_ += len;

            if (BodyLen < HeaderLen)
            {
                Logger.Debug("bodylen < header len");
                pkg = default;
                return false;
            }
            else
            {
                var pkgLen = BitConverter.ToUInt16(buffer_, 0);

                if (BodyLen == pkgLen)
                {                   
                    Logger.Debug("bodylen == pkgLen");
                    head_ = tail_ = 0;
                    pkg = GetPackage();
                    return true;
                }
                else if (BodyLen < pkgLen)
                {
                    Logger.Debug("bodylen < pkgLen");
                    pkg = default;
                    return false;
                }
                else if (BodyLen > pkgLen)
                {
                    Logger.Debug("bodylen > pkgLen");
                    head_ += pkgLen;
                    pkg = GetPackage();
                    return true;
                }
            }

            pkg = default;
            return false;
        }

        private Package GetPackage()
        {
            int pos = 0;
            var pkgLen = BitConverter.ToUInt16(buffer_, 0);
            pos += 2;
            var pkgID = BitConverter.ToUInt32(buffer_, pos);
            pos += 4;
            var pkgVersion = BitConverter.ToUInt16(buffer_, pos);
            pos += 2;
            var pkgType = BitConverter.ToUInt16(buffer_, pos);

            var pkg = new Package();

            pkg.Header.Length = pkgLen;
            pkg.Header.ID = pkgID;
            pkg.Header.Version = pkgVersion;
            pkg.Header.Type = pkgType;

            return pkg;
        }

    }
}
