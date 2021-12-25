using System;

namespace LPS.Core.IPC
{
    public class Message
    {
        public readonly IComparable Key;
        public readonly object[] args;

        public Message(IComparable key, object[] args)
        {
            Key = key;
            this.args = args;
        }
    }
}
