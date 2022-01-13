using System;

namespace LPS.Core.Ipc
{
    public class Message
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
