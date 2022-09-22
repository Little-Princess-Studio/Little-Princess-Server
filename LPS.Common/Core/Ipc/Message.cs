namespace LPS.Common.Core.Ipc
{
    public struct Message
    {
        public readonly IComparable Key;
        public readonly object Arg;

        public Message(IComparable key, object arg)
        {
            Key = key;
            this.Arg = arg;
        }
    }
}
