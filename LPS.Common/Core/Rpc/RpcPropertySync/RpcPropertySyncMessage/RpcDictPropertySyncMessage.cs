namespace LPS.Common.Core.Rpc.RpcPropertySync.RpcPropertySyncMessage
{
    using Google.Protobuf.WellKnownTypes;
    using InnerMessages;
    using RpcProperty.RpcContainer;
    using RpcPropertySyncInfo;
    using MailBox = Rpc.MailBox;

    interface IRpcDictPropertySyncMessageImpl : IRpcPropertySyncMessageImpl
    {
        bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo);
        bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg);
    }

    public class RpcDictPropertySetValueSyncMessageImpl : IRpcDictPropertySyncMessageImpl
    {
        private RpcPropertyContainer? value_;

        public void SetValue(RpcPropertyContainer value)
        {
            value_ = value;
        }
        
        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.SetValue
            };
            
            cmd.Args.Add(value_!.ToRpcArg());

            return cmd;
        }

        public bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo)
        {   
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.SetValue)
            {
                var setValueImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertySetValueSyncMessageImpl>();
                setValueImpl.value_ = value_;
                return true;
            }

            return true;
        }

        public bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            var setValueImpl = newMsg.GetImpl<RpcDictPropertySetValueSyncMessageImpl>();
            value_ = setValueImpl.value_;

            return true;
        }
    }
    
    public class RpcDictPropertyUpdatePairSyncMessageImpl : IRpcDictPropertySyncMessageImpl
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

            if (lastMsg.Operation == RpcPropertySyncOperation.UpdatePair)
            {
                var updateImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyUpdatePairSyncMessageImpl>();
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
            if (newMsg.Operation != RpcPropertySyncOperation.UpdatePair)
            {
                return false;
            }

            var dictUpdateImpl = newMsg.GetImpl<RpcDictPropertyUpdatePairSyncMessageImpl>();
            var dictUpdateInfo = dictUpdateImpl.GetUpdateDictInfo();
            foreach (var (key, value) in dictUpdateInfo)
            {
                updateDictInfo_[key] = value;
            }

            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.UpdatePair
            };

            var dictWithStringKeyArg = new DictWithStringKeyArg();
            foreach (var (key, value) in updateDictInfo_)
            {
                dictWithStringKeyArg.PayLoad.Add(key, value.ToRpcArg());
            }

            cmd.Args.Add(Any.Pack(dictWithStringKeyArg));
            return cmd;
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

            if (lastMsg.Operation == RpcPropertySyncOperation.UpdatePair)
            {
                var updateImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyUpdatePairSyncMessageImpl>();
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

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.RemoveElem
            };

            foreach (var key in removeDictInfo_)
            {
                cmd.Args.Add(Any.Pack(new StringArg()
                {
                    PayLoad = key
                }));
            }

            return cmd;
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
                case RpcPropertySyncOperation.UpdatePair:
                    impl_ = new RpcDictPropertyUpdatePairSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.RemoveElem:
                    impl_ = new RpcDictPropertyRemoveSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.Clear:
                    break;
                case RpcPropertySyncOperation.SetValue:
                    impl_ = new RpcDictPropertySetValueSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.AddListElem:
                case RpcPropertySyncOperation.InsertElem:
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }

            this.Action = operation switch
            {
                RpcPropertySyncOperation.UpdatePair => args =>
                {
                    var key = args[0] as string;
                    var val = args[1] as RpcPropertyContainer;

                    if (val == null)
                    {
                        throw new Exception($"Invalid args {args}");
                    }

                    ((RpcDictPropertyUpdatePairSyncMessageImpl) impl_!).Update(key!, val);
                },
                RpcPropertySyncOperation.RemoveElem => args =>
                {
                    var key = args[0] as string;

                    ((RpcDictPropertyRemoveSyncMessageImpl) impl_!).Remove(key!);
                },
                RpcPropertySyncOperation.SetValue => args =>
                {
                    var val = args[0] as RpcPropertyContainer;
                    ((RpcDictPropertySetValueSyncMessageImpl) impl_!).SetValue(val!);
                },
                RpcPropertySyncOperation.Clear => args => { },
                _ => throw new Exception($"Invalid operation {operation}")
            };
        }

        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg) =>
            impl_!.MergeKeepOrder((otherMsg as RpcDictPropertySyncMessage)!);

        public override void MergeIntoSyncInfo(RpcPropertySync.RpcPropertySyncInfo.RpcPropertySyncInfo rpcPropertySyncInfo)
        {
            var dictInfo = (rpcPropertySyncInfo as RpcDictPropertySyncInfo)!;

            // if we got a clear msg,
            // then cancel all the previous modification and record the clear msg
            if (this.Operation == RpcPropertySyncOperation.Clear || this.Operation == RpcPropertySyncOperation.SetValue)
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

        public T GetImpl<T>() => (T) impl_!;

        public override PropertySyncCommand Serialize()
        {
            if (this.Operation == RpcPropertySyncOperation.Clear)
            {
                return new PropertySyncCommand()
                {
                    Operation = SyncOperation.Clear
                };
            }

            return impl_!.ToSyncCommand();
        }
    }
}