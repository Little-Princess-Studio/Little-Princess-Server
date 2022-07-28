using LPS.Common.Core.Debug;
using LPS.Common.Core.Rpc.InnerMessages;

namespace LPS.Server.Core.Rpc.InnerMessages
{
    public class MessageBuffer
    {
        // initial 4k buffer to recv network messages
        const int InitBufLength = 2048;
        private static readonly int HeaderLen_ = PackageHeader.Size;

        private int head_;
        private int tail_;

        private int BodyLen=> tail_ - head_;

        private int curBufLen_ = InitBufLength;

        private byte[] buffer_ = new byte[InitBufLength];

        public bool TryReceiveFromRaw(byte[] incomeBuffer, int len, out Package pkg)
        {
            /*
            How to handle TCP stream raw data to Package:

            1. if tail_+len-1 >= current len
                if bodylen + len >= current len
                    double current buf, copy buf[head..tail] to new[0..bodylen], buf = new, tail_ = tail_ - head_, head_ = 0
                else
                    copy buf[head_..tail_] to buf[0..bodylen], head_ = 0, tail_ = bodylen

            2. copy incomeBuffer[0..len] to buf[tail_..tail+len]

            3. if bodylen < header, do nothing, wait next data
                else
                    header = pick_header_from_raw_data(buf[head..tail])
                    if bodylen == header.package_len then get package from buf[head..tail], reset head = tail = 0
                    if bodylen < header.package_len then wait next data
                    if bodylen > header.package_len then get package from buf[head..head+header.package_len], set head = head + header.package_len
            */

            if (tail_+len > curBufLen_)
            {
                if (BodyLen + len > curBufLen_)
                {
                    while (tail_+len-1>= curBufLen_)
                    {
                        Logger.Debug($"{tail_} + {len} - 1 = {tail_ + len - 1} >= {curBufLen_}, double the buf");
                        // repeat double size
                        curBufLen_ <<= 1;
                    }

                    byte[] newBuffer = new byte[curBufLen_];

                    // copy current data buf[head...tail] -> new[0...tail-head]
                    Buffer.BlockCopy(buffer_, head_, newBuffer, 0, BodyLen);
                    buffer_ = newBuffer;

                    tail_ = BodyLen;
                    head_ = 0;
                }
                else
                {
                    Buffer.BlockCopy(buffer_, head_, buffer_, 0, BodyLen);

                    // Logger.Debug($"move to head {head_} {BodyLen}");

                    tail_ = BodyLen;
                    head_ = 0;
                }

            }

            // copy incomeBuffer to buf [tail_..tail_+len]
            Buffer.BlockCopy(incomeBuffer, 0, buffer_, tail_, len);
            tail_ += len;

            if (BodyLen < HeaderLen_)
            {
                // Logger.Debug("bodylen < header len");
                pkg = default;
                return false;
            }
            else
            {
                var pkgLen = BitConverter.ToUInt16(buffer_, head_);

                // Logger.Debug($"bodylen={BodyLen}, pkglen={pkgLen}"); 

                if (BodyLen == pkgLen)
                {                   
                    // Logger.Debug("bodylen == pkgLen");
                    pkg = GetPackage();
                    head_ = tail_ = 0;
                    return true;
                }
                else if (BodyLen < pkgLen)
                {
                    // Logger.Debug("bodylen < pkgLen");
                    pkg = default;
                    return false;
                }
                else if (BodyLen > pkgLen)
                {
                    // Logger.Debug("bodylen > pkgLen");
                    pkg = GetPackage();
                    head_ += pkgLen;
                    return true;
                }
            }

            pkg = default;
            return false;
        }

        private Package GetPackage()
        {
            int pos = head_;
            var pkgLen = BitConverter.ToUInt16(buffer_, pos);
            pos += 2;
            var pkgId = BitConverter.ToUInt32(buffer_, pos);
            pos += 4;
            var pkgVersion = BitConverter.ToUInt16(buffer_, pos);
            pos += 2;
            var pkgType = BitConverter.ToUInt16(buffer_, pos);

            var pkg = new Package();

            // Logger.Debug($"get pkg len: {pkgLen}, id: {pkgID}, version: {pkgVersion}, type: {pkgType}");

            pkg.Header.Length = pkgLen;
            pkg.Header.ID = pkgId;
            pkg.Header.Version = pkgVersion;
            pkg.Header.Type = pkgType;

            pkg.Body = new byte[pkgLen - HeaderLen_];

            // Logger.Debug($"buffer_ size: {buffer_.Length}, {head_ + HeaderLen}, {pkgLen - HeaderLen}");

            Buffer.BlockCopy(buffer_, head_ + HeaderLen_, pkg.Body, 0, pkgLen - HeaderLen_);

            return pkg;
        }

    }
}
