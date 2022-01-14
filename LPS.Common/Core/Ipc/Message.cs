using System;

namespace LPS.Core.Ipc
{
    public struct Message
    {
        public readonly IComparable Key;
        public readonly object arg;

        public Message(IComparable key, object arg)
        {
            Key = key;
            this.arg = arg;
        }
    }
}
