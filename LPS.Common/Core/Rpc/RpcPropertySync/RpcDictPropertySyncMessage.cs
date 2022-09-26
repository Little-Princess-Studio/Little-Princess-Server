using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.RpcProperty;

namespace LPS.Common.Core.Rpc.RpcPropertySync
{
    interface IRpcDictPropertySyncMessageImpl
    {
        bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo);
        bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg);
    }

    public class RpcDictPropertyUpdateSyncMessageImpl : IRpcDictPropertySyncMessageImpl
    {
        private readonly Dictionary<string, RpcPropertyContainer> updateDictInfo_ = new();
        public Dictionary<string, RpcPropertyContainer> GetUpdateDictInfo() => updateDictInfo_;

        public void Update(string key, RpcPropertyContainer value)
        {
            updateDictInfo_[key] = value;
        }

        public bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo)
        {
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.UpdateDict)
            {
                var updateImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyUpdateSyncMessageImpl>();
                foreach (var (key, value) in updateDictInfo_)
                {
                    updateImpl.updateDictInfo_[key] = value;
                }

                return true;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.RemoveElem)
            {
                var removeImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
                var removedKeys = removeImpl.GetRemoveDictInfo();
                foreach (var (key, value) in updateDictInfo_)
                {
                    if (removedKeys.Contains(key))
                    {
                        removedKeys.Remove(key);
                    }
                }

                if (removedKeys.Count == 0)
                {
                    rpcDictPropertySyncInfo.PopLastMsg();
                    return false;
                }
            }

            return true;
        }

        public bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.UpdateDict)
            {
                return false;
            }

            var dictUpdateImpl = newMsg.GetImpl<RpcDictPropertyUpdateSyncMessageImpl>();
            var dictUpdateInfo = dictUpdateImpl.GetUpdateDictInfo();
            foreach (var (key, value) in dictUpdateInfo)
            {
                updateDictInfo_[key] = value;
            }

            return true;
        }
    }

    public class RpcDictPropertyRemoveSyncMessageImpl : IRpcDictPropertySyncMessageImpl
    {
        private readonly HashSet<string> removeDictInfo_ = new();
        public HashSet<string> GetRemoveDictInfo() => removeDictInfo_;

        public void Remove(string key)
        {
            removeDictInfo_.Add(key);
        }

        public bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo)
        {
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.RemoveElem)
            {
                var removeImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
                foreach (var key in removeDictInfo_)
                {
                    removeImpl.removeDictInfo_.Add(key);
                }

                return true;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.UpdateDict)
            {
                var updateImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyUpdateSyncMessageImpl>();
                var updateInfo = updateImpl.GetUpdateDictInfo();
                foreach (var key in removeDictInfo_)
                {
                    if (updateInfo.ContainsKey(key))
                    {
                        updateInfo.Remove(key);
                    }
                }

                if (updateInfo.Count == 0)
                {
                    rpcDictPropertySyncInfo.PopLastMsg();
                    // if return false, this msg will be enqued
                    return false;
                }
            }

            return true;
        }

        public bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.RemoveElem)
            {
                return false;
            }

            var dictRemoveImpl = newMsg.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
            var dictRemoveInfo = dictRemoveImpl.GetRemoveDictInfo();
            foreach (var key in dictRemoveInfo)
            {
                removeDictInfo_.Add(key);
            }

            return true;
        }
    }


    public class RpcDictPropertySyncMessage : RpcPropertySyncMessage
    {
        private readonly IRpcDictPropertySyncMessageImpl? impl_;

        public delegate void DictOperation(params object[] args);

        public DictOperation? Action { get; }

        public RpcDictPropertySyncMessage(MailBox mailbox,
            RpcPropertySyncOperation operation,
            string rpcPropertyPath)
            : base(mailbox, operation, rpcPropertyPath, RpcSyncPropertyType.Dict)
        {
            switch (operation)
            {
                case RpcPropertySyncOperation.UpdateDict:
                    impl_ = new RpcDictPropertyUpdateSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.RemoveElem:
                    impl_ = new RpcDictPropertyRemoveSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.Clear:
                    break;
                case RpcPropertySyncOperation.SetValue:
                case RpcPropertySyncOperation.AddListElem:
                case RpcPropertySyncOperation.InsertElem:
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }

            this.Action = operation switch
            {
                RpcPropertySyncOperation.UpdateDict => args =>
                {
                    var key = args[0] as string;
                    var val = args[1] as RpcPropertyContainer;

                    if (val == null)
                    {
                        throw new Exception($"Invalid args {args}");
                    }

                    ((RpcDictPropertyUpdateSyncMessageImpl) impl_!).Update(key!, val);
                },
                RpcPropertySyncOperation.RemoveElem => args =>
                {
                    var key = args[0] as string;

                    ((RpcDictPropertyRemoveSyncMessageImpl) impl_!).Remove(key!);
                },
                RpcPropertySyncOperation.Clear => args => { },
                _ => throw new Exception($"Invalid operation {operation}")
            };
        }

        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg) =>
            impl_!.MergeKeepOrder((otherMsg as RpcDictPropertySyncMessage)!);

        public override void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo)
        {
            var dictInfo = (rpcPropertySyncInfo as RpcDictPropertySyncInfo)!;

            // if we got a clear msg,
            // then cancel all the previous modification and record the clear msg
            if (this.Operation == RpcPropertySyncOperation.Clear)
            {
                dictInfo.PropPath2SyncMsgQueue.Clear();
                dictInfo.Enque(this);
                return;
            }
            
            if (dictInfo.PropPath2SyncMsgQueue.Count == 0)
            {
                dictInfo.Enque(this);
            }
            else
            {
                var res = impl_!.MergeIntoSyncInfo(dictInfo);
                if (!res)
                {
                    dictInfo.Enque(this);
                }
            }
        }

        // private void HandleUpdateDict(RpcDictPropertySyncMessage curDictSyncInfo)
        // {
        //     curDictSyncInfo.updateDictInfo_ ??= updateDictInfo_!;
        //     
        //     foreach (var (key, value) in updateDictInfo_!)
        //     {
        //         curDictSyncInfo.updateDictInfo_[key] = value;
        //         if (curDictSyncInfo.removeDictInfo_ != null
        //             && curDictSyncInfo.removeDictInfo_.Contains(key))
        //         {
        //             curDictSyncInfo.removeDictInfo_.Remove(value);
        //         }
        //     }
        //
        //     if (curDictSyncInfo.removeDictInfo_ is {Count: 0})
        //     {
        //         curDictSyncInfo.removeDictInfo_ = null;
        //     }
        // }
        //
        // private void HandleRemove(RpcDictPropertySyncMessage curDictSyncInfo)
        // {
        //     curDictSyncInfo.removeDictInfo_ ??= removeDictInfo_!;
        //
        //     foreach (var key in removeDictInfo_!)
        //     {
        //         curDictSyncInfo.removeDictInfo_.Add(key);
        //         if (curDictSyncInfo.updateDictInfo_ != null
        //             && curDictSyncInfo.updateDictInfo_.ContainsKey(key))
        //         {
        //             curDictSyncInfo.updateDictInfo_.Remove(key);
        //         }
        //     }
        //
        //     if (curDictSyncInfo.updateDictInfo_ is {Count: 0})
        //     {
        //         curDictSyncInfo.updateDictInfo_ = null;
        //     }
        // }

        public T GetImpl<T>() => (T) impl_!;

        public override byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}