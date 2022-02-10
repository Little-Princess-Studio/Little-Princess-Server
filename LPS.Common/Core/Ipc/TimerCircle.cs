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

        private uint currentTimestamp_;
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
            currentTimestamp_ = 0;
            slotIndex_ = 0;
        }

        public void Tick(uint duration)
        {
            var moveStep = duration / timeInterval_;

            for (int i = 0; i < moveStep; ++i, ++slotIndex_)
            {
                if (slotIndex_ >= slotsPerCircle_)
                {
                    // todo: fill slots
                    break;
                }
                var slot = slots_[slotIndex_];
                slot.Dispatch();
                slot.Clear();
            }
            
            currentTimestamp_ += duration;
        }
    }
}