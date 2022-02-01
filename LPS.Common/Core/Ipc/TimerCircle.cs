using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LPS.Core.Ipc
{
    public class TimeCircleSlot
    {
        public const int MaxSlotMessageSize = 2048;
        private int slotMassageCount_ = 0;

        private readonly Dictionary<string, Queue<RpcPropertySyncMessage>> idToSyncMsg_ = new();
        bool Full() => slotMassageCount_ >= MaxSlotMessageSize;
        void Clear() => idToSyncMsg_.Clear();

        private void IncreaseMessageCount() {
            lock(this)
            {
                ++slotMassageCount_;
            }
        }

        public void AddSyncMessage(in RpcPropertySyncMessage incomeMsg)
        {
            var id = incomeMsg.MailBox.Id;
            if (idToSyncMsg_.ContainsKey(id))
            {
                var queue = idToSyncMsg_[id];
                var last = queue.Last();
                var mergeSucc = last.Merge(incomeMsg);
                if (!mergeSucc)
                {
                    queue.Enqueue(incomeMsg);
                }
            }
            else
            {
                var queue = new Queue<RpcPropertySyncMessage>();
                queue.Enqueue(incomeMsg);
                idToSyncMsg_[id] = queue;
            }
        }
    }

    public class TimerCircle
    {
        private readonly int timeInterval_;
        private readonly int totalMillisecondsPerCircle_;

        private readonly Queue<object> waitingMessageQueue_;
        private readonly TimeCircleSlot[] slots_;

        public TimerCircle(int timeIntervalByMillisecond, int totalMillisecondsPerCircle)
        {
            if (1000 % timeIntervalByMillisecond != 0)
            {
                throw new Exception("Error time interval for time circle.");
            }

            timeInterval_ = timeIntervalByMillisecond;
            totalMillisecondsPerCircle_ = totalMillisecondsPerCircle;
            waitingMessageQueue_ = new();
            slots_ = new TimeCircleSlot[totalMillisecondsPerCircle_ / timeInterval_];
        }

        public void Tick(int duration)
        {
        }
    }
}