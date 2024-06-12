// -----------------------------------------------------------------------
// <copyright file="MessageBuffer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.InnerMessages;

using LPS.Common.Debug;

/// <summary>
/// Message buffer for cache the binary message received,
/// and used for message dispatcher to parse the protobuf message.
/// Message buffer will automatically double its size if current capacity is not enough to hold new message.
/// </summary>
public class MessageBuffer
{
    // initial 4k buffer to recv network messages
    private const int InitBufLength = 2048;
    private static readonly int HeaderLen = PackageHeader.Size;

    private int head;
    private int tail;

    private int BodyLen => this.tail - this.head;

    private int curBufLen = InitBufLength;

    private Memory<byte> buffer = new byte[InitBufLength];

    /// <summary>
    /// How to handle TCP stream raw data to Package:
    /// 1. if tail_+len-1 >= current len
    /// if bodylen + len >= current len
    /// double current buf, copy buf[head..tail] to new[0..bodylen], buf = new, tail_ = tail_ - head_, head_ = 0
    /// else
    /// copy buf[head_..tail_] to buf[0..bodylen], head_ = 0, tail_ = bodylen
    /// 2. copy incomeBuffer[0..len] to buf[tail_..tail+len]
    /// 3. if bodylen less than header, do nothing, wait next data
    /// else
    /// header = pick_header_from_raw_data(buf[head..tail])
    /// if bodylen == header.package_len then get package from buf[head..tail], reset head = tail = 0
    /// if bodylen less than header.package_len then wait next data
    /// if bodylen larger than header.package_len then get package from buf[head..head+header.package_len], set head = head + header.package_len.
    /// </summary>
    /// <param name="incomeBuffer">New package array.</param>
    /// <param name="len">Length of the new package array.</param>
    /// <param name="pkg">Parsed package.</param>
    /// <returns>If succeed to receive a package from buffer, return true otherwise false.</returns>
    public bool TryReceiveFromRaw(Span<byte> incomeBuffer, int len, out Package pkg)
    {
        if (this.tail + len > this.curBufLen)
        {
            if (this.BodyLen + len > this.curBufLen)
            {
                while (this.tail + len - 1 >= this.curBufLen)
                {
                    Logger.Debug(
                        $"{this.tail} + {len} - 1 = {this.tail + len - 1} >= {this.curBufLen}, double the buf");

                    // repeat double size
                    this.curBufLen <<= 1;
                }

                Memory<byte> newBuffer = new byte[this.curBufLen];

                // copy current data buf[head...tail] -> new[0...tail-head]
                // Buffer.BlockCopy(this.buffer, this.head, newBuffer, 0, this.BodyLen);
                this.buffer[this.head..this.tail].CopyTo(newBuffer);
                this.buffer = newBuffer;

                this.tail = this.BodyLen;
                this.head = 0;
            }
            else
            {
                // Buffer.BlockCopy(this.buffer, this.head, this.buffer, 0, this.BodyLen);
                this.buffer[this.head..this.tail].CopyTo(this.buffer);

                this.tail = this.BodyLen;
                this.head = 0;
            }
        }

        // copy incomeBuffer to buf [tail_..tail_+len]
        incomeBuffer[0..len].CopyTo(this.buffer.Span[this.tail..(this.tail + len)]);

        // Buffer.BlockCopy(incomeBuffer, 0, this.buffer, this.tail, len);
        this.tail += len;

        if (this.BodyLen < HeaderLen)
        {
            pkg = default;
            return false;
        }
        else
        {
            var pkgBuffer = this.buffer.Span[this.head..];
            var pkgLen = BitConverter.ToUInt16(pkgBuffer);

            // Logger.Debug($"bodylen={BodyLen}, pkglen={pkgLen}");
            if (this.BodyLen == pkgLen)
            {
                pkg = PackageHelper.GetPackage(pkgBuffer);
                this.head = this.tail = 0;
                return true;
            }
            else if (this.BodyLen < pkgLen)
            {
                pkg = default;
                return false;
            }
            else if (this.BodyLen > pkgLen)
            {
                pkg = PackageHelper.GetPackage(pkgBuffer);
                this.head += pkgLen;
                return true;
            }
        }

        pkg = default;
        return false;
    }
}