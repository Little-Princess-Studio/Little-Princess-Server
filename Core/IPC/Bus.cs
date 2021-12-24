using System.Collections.Concurrent;

namespace LPS.Core.IPC
{
    public class Bus
    {
        private ConcurrentQueue<Message> m_msgQueue;

        public bool Empty => m_msgQueue.IsEmpty;

        public void AppendMessage(Message msg)
        {
            m_msgQueue.Enqueue(msg);
        }

        public bool TryDeque(out Message msg)
        {
            return m_msgQueue.TryDequeue(out msg);
        }
    }
}
