using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcPropertySync;

namespace LPS.Core.Ipc
{
    public class TimeCircleSlot
    {
        public const int MaxSlotMessageSize = 2048;
        private int slotMassageCount_;

        // entityID => { propertyPath => RpcPropertySyncMessage }
        private readonly Dictionary<string, Dictionary<string, RpcPropertySyncInfo>> idToSyncMsg_ = new();
        private readonly Dictionary<string, Queue<RpcPropertySyncMessage>> idToSyncMsgWithOrder_ = new();
        public bool Full() => slotMassageCount_ >= MaxSlotMessageSize;

        public void Clear()
        {
            idToSyncMsg_.Clear();
            idToSyncMsgWithOrder_.Clear();
            slotMassageCount_ = 0;
        }

        private void IncreaseMessageCount() {
            lock(this)
            {
                ++slotMassageCount_;
            }
        }

        private RpcPropertySyncInfo GetRpcPropertySyncInfo(
            MailBox mb, string path, Func<RpcPropertySyncInfo> getSyncInfoFunc)
        {
            var id = mb.Id;
            if (idToSyncMsg_.ContainsKey(id))
            {
                var entitySyncInfoDict = idToSyncMsg_[id];
                if (entitySyncInfoDict.ContainsKey(path))
                {
                    var syncInfo = entitySyncInfoDict[path];
                    return syncInfo;
                }

                var newSyncInfo = getSyncInfoFunc();
                entitySyncInfoDict[path] = newSyncInfo;
                return newSyncInfo;
            }
            else
            {
                var newEntitySyncInfoDict = new Dictionary<string, RpcPropertySyncInfo>();
                var newSyncInfo = getSyncInfoFunc();
                idToSyncMsg_[id] = newEntitySyncInfoDict;
                newEntitySyncInfoDict[path] = newSyncInfo;
                return newSyncInfo;
            }
        }

        public void AddSyncMessageKeepOrder(RpcPropertySyncMessage incomeMsg)
        {
            var id = incomeMsg.MailBox.Id;

            Queue<RpcPropertySyncMessage> queue;
            if (idToSyncMsg_.ContainsKey(id))
            {
                queue = idToSyncMsgWithOrder_[id];
            }
            else
            {
                queue = new Queue<RpcPropertySyncMessage>();
                idToSyncMsgWithOrder_[id] = queue;
            }

            if (queue.Count > 0)
            {
                var lastMsg = queue.Last();
                var res = lastMsg.MergeKeepOrder(incomeMsg);
                if (res)
                {
                    return;
                }
            }
            queue.Enqueue(incomeMsg);
        }

        public void AddSyncMessageNoKeepOrder(RpcPropertySyncMessage incomeMsg)
        {
            // var syncInfo = this.GetRpcPropertySyncInfo(incomeMsg.MailBox, incomeMsg.RpcPropertyPath, ())
            var propChangeOperation = incomeMsg.Operation;

            Func<RpcPropertySyncInfo> getSyncInfoFunc = incomeMsg.RpcSyncPropertyType switch
            {
                RpcSyncPropertyType.Plaint => () => new RpcPlaintPropertySyncInfo(),
                RpcSyncPropertyType.List => () => new RpcListPropertySyncInfo(),
                RpcSyncPropertyType.Dict => () => new RpcDictPropertySyncInfo(),
                _ => throw new ArgumentOutOfRangeException()
            };

            var syncInfo = this.GetRpcPropertySyncInfo(
                incomeMsg.MailBox,
                incomeMsg.RpcPropertyPath,
                getSyncInfoFunc
            );
            
            syncInfo.AddNewSyncMessage(incomeMsg);; 
        }

        public void Dispatch()
        {
            
        }
    }

    public class TimerCircle
    {
        private readonly int timeInterval_;
        private readonly int totalMillisecondsPerCircle_;

        private readonly Queue<(uint, RpcPropertySyncMessage)> waitingMessageQueue_;
        private readonly TimeCircleSlot[] slots_;

        private int slotIndex_;
        private readonly int slotsPerCircle_;

        public TimerCircle(int timeIntervalByMillisecond, int totalMillisecondsPerCircle)
        {
            if (1000 % timeIntervalByMillisecond != 0)
            {
                throw new Exception("Error time interval for time circle.");
            }

            timeInterval_ = timeIntervalByMillisecond;
            totalMillisecondsPerCircle_ = totalMillisecondsPerCircle;
            waitingMessageQueue_ = new();
            slotsPerCircle_ = totalMillisecondsPerCircle_ / timeInterval_;
            slots_ = new TimeCircleSlot[slotsPerCircle_];
        }

        public void Start()
        {
            slotIndex_ = 0;
        }

        public void FillSlot(TimeCircleSlot slot, int slotIndex)
        {
            slot.Clear();
            var targetTime = (uint)(slotIndex)* timeInterval_;
            do
            {
                
            } while (waitingMessageQueue_);
        }

        // dispatch 0 -> fill 60 to 0
        // dispatch 1 -> fill 61 to 1
        // dispatch n -> fill n + 60 to n
        public void Tick(uint duration)
        {
            // move forward
            var moveStep = duration / timeInterval_;

            for (int i = 0; i < moveStep; i++)
            {
                ++slotIndex_;
                var slotCircleIndex = slotIndex_ % slotsPerCircle_;
                var slot = slots_[slotCircleIndex];
                
                slot.Dispatch();
                this.FillSlot(slot, slotIndex_ + slotsPerCircle_);
            }
        }
    }
}