using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcPropertySync;

namespace LPS.Core.Ipc
{
    public class TimeCircleSlot
    {
        // public const int MaxSlotMessageSize = 2048;
        private int slotMassageCount_;

        // entityID => { propertyPath => RpcPropertySyncMessage }
        private readonly Dictionary<string, Dictionary<string, RpcPropertySyncInfo>> idToSyncMsg_ = new();
        private readonly Dictionary<string, Queue<RpcPropertySyncMessage>> idToSyncMsgWithOrder_ = new();

        // public bool Full() => slotMassageCount_ >= MaxSlotMessageSize;

        public void Clear()
        {
            idToSyncMsg_.Clear();
            idToSyncMsgWithOrder_.Clear();
            slotMassageCount_ = 0;
        }

        // private void IncreaseMessageCount() {
        //     lock(this)
        //     {
        //         ++slotMassageCount_;
        //     }
        // }

        public Queue<RpcPropertySyncMessage>? FindOrderedSyncQueue(MailBox mb) =>
            idToSyncMsgWithOrder_.ContainsKey(mb.Id) ? idToSyncMsgWithOrder_[mb.Id] : null;

        public int GetSyncQueueLength(MailBox mb) =>
            idToSyncMsgWithOrder_.ContainsKey(mb.Id) ? idToSyncMsgWithOrder_[mb.Id].Count : 0;

        public RpcPropertySyncInfo? FindRpcPropertySyncInfo(MailBox mb, string path)
        {
            var id = mb.Id;
            if (!idToSyncMsg_.ContainsKey(id))
            {
                return null;
            }

            var info = idToSyncMsg_[id];

            if (!info.ContainsKey(path))
            {
                return null;
            }

            return info[path];
        }

        public RpcPropertySyncInfo GetRpcPropertySyncInfo(
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
            
            lock (this)
            {
                Queue<RpcPropertySyncMessage> queue;

                if (idToSyncMsgWithOrder_.ContainsKey(id))
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
        }

        public void AddSyncMessageNoKeepOrder(RpcPropertySyncMessage incomeMsg)
        {
            lock (this)
            {
                Func<RpcPropertySyncInfo> getSyncInfoFunc = incomeMsg.RpcSyncPropertyType switch
                {
                    RpcSyncPropertyType.PlaintAndCostume => () => new RpcPlaintAndCostumePropertySyncInfo(),
                    RpcSyncPropertyType.List => () => new RpcListPropertySyncInfo(),
                    RpcSyncPropertyType.Dict => () => new RpcDictPropertySyncInfo(),
                    _ => throw new ArgumentOutOfRangeException()
                };

                var syncInfo = this.GetRpcPropertySyncInfo(
                    incomeMsg.MailBox,
                    incomeMsg.RpcPropertyPath,
                    getSyncInfoFunc
                );

                syncInfo.AddNewSyncMessage(incomeMsg);
            }
        }

        public void Dispatch()
        {
            Logger.Debug("Dispatch Message");
            foreach (var (entityId, entitySyncInfoDict) in idToSyncMsg_)
            {
                Logger.Debug($"Dispatch entity id {entityId}");
                foreach (var (propPath, syncInfo) in entitySyncInfoDict)
                {
                    Logger.Debug($"{propPath} -> {syncInfo}");
                }
            }

            foreach (var (entityId, msgQueue) in idToSyncMsgWithOrder_)
            {
                Logger.Debug($"Dispatch entity id {entityId}");
                foreach (var (propPath, syncQueue) in idToSyncMsgWithOrder_)
                {
                    while (syncQueue.Count > 0)
                    {
                        var msg = syncQueue.Dequeue();
                        Logger.Debug($"{propPath} -> {msg}");
                    }
                }
            }
        }
    }

    public class TimeCircle
    {
        private readonly int timeInterval_;
        // private readonly int totalMillisecondsPerCircle_;

        private readonly ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)> waitingMessageQueue_;
        private readonly TimeCircleSlot[] slots_;

        private int slotIndex_;
        private readonly int slotsPerCircle_;

        public TimeCircle(int timeIntervalByMillisecond, int totalMillisecondsPerCircle)
        {
            if (1000 % timeIntervalByMillisecond != 0)
            {
                throw new Exception("Error time interval for time circle.");
            }

            timeInterval_ = timeIntervalByMillisecond;
            // totalMillisecondsPerCircle_ = totalMillisecondsPerCircle;
            waitingMessageQueue_ = new ConcurrentQueue<(bool, uint, RpcPropertySyncMessage)>();
            slotsPerCircle_ = totalMillisecondsPerCircle / timeInterval_;
            slots_ = new TimeCircleSlot[slotsPerCircle_];
            for (int i = 0; i < slotsPerCircle_; ++i)
            {
                slots_[i] = new TimeCircleSlot();
            }
        }

        public void Start()
        {
            slotIndex_ = 0;
        }

        public void AddPropertySyncMessage(RpcPropertySyncMessage msg, uint delayTimeByMillisecond, bool keepOrder)
        {
            // can arrange directly
            if (delayTimeByMillisecond <= slotsPerCircle_ * timeInterval_)
            {
                var arrangeSlot = (slotIndex_ +
                                   (uint) Math.Floor(delayTimeByMillisecond / (decimal) timeInterval_))
                                  % slotsPerCircle_;
                if (keepOrder)
                {
                    slots_[arrangeSlot].AddSyncMessageKeepOrder(msg);
                }
                else
                {
                    slots_[arrangeSlot].AddSyncMessageNoKeepOrder(msg);
                }
            }
            // arrange to waiting queue
            else
            {
                var arrangeTime = (uint) slotIndex_ * (uint) timeInterval_ + delayTimeByMillisecond;
                waitingMessageQueue_.Enqueue((keepOrder, arrangeTime, msg));
            }
        }

        public void FillSlot(TimeCircleSlot slot, int slotIndex)
        {
            slot.Clear();
            var targetEndTime = (uint) (slotIndex + 1) * timeInterval_;
            do
            {
                if (waitingMessageQueue_.Count == 0)
                {
                    break;
                }

                var res = waitingMessageQueue_.TryPeek(out var candidate);
                if (!res)
                {
                    break;
                }

                var (_, msgDispatchTime, _) = candidate;
                if (msgDispatchTime <= targetEndTime)
                {
                    waitingMessageQueue_.TryDequeue(out candidate);
                    var (keepOrder, _, msg) = candidate;
                    if (keepOrder)
                    {
                        slot.AddSyncMessageKeepOrder(msg);
                    }
                    else
                    {
                        slot.AddSyncMessageNoKeepOrder(msg);
                    }
                }
                else
                {
                    break;
                }
            } while (true);
        }

        // dispatch 0 [0...50] -> fill 60 to 0
        // dispatch 1 [51...100] -> fill 61 to 1
        // dispatch n [101 ... 150] -> fill n + 60 to n
        public void Tick(uint duration)
        {
            // move forward
            var moveStep = duration / timeInterval_;

            for (int i = 0; i < moveStep; i++)
            {
                var slotCircleIndex = slotIndex_ % slotsPerCircle_;
                var slot = slots_[slotCircleIndex];

                slot.Dispatch();
                this.FillSlot(slot, slotIndex_ + slotsPerCircle_);
                ++slotIndex_;
            }
        }
    }
}