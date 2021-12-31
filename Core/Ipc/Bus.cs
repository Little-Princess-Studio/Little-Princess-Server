using System.Collections.Concurrent;

namespace LPS.Core.Ipc
{
    public class Bus
    {
        private readonly ConcurrentQueue<Message> msgQueue_ = new();
        private readonly Dispatcher dispatcher_;

        public Bus(Dispatcher dispatcher)
        {
            this.dispatcher_ = dispatcher;
        }

        private bool Empty => msgQueue_.IsEmpty;

        public void AppendMessage(Message msg)
        {
            msgQueue_.Enqueue(msg);
        }

        private bool TryDeque(out Message msg)
        {
            return msgQueue_.TryDequeue(out msg!);
        }

        public void Pump()
        {
            if (this.Empty)
            {
                return;
            }

            bool succ = this.TryDeque(out var msg);

            if (!succ)
            {
                return;
            }

            do
            {
                dispatcher_.Dispatch(msg.Key, msg.arg);
                succ = this.TryDeque(out msg);
            } while (succ);
        }
    }
}
