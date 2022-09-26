using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcProperty;

// operation on List is very complex, so RpcListPropertySyncInfo
// will only merge same operation for a continuous operation sequence

namespace LPS.Common.Core.Rpc.RpcPropertySync
{
    interface IRpcPropertySyncMessageImpl
    {
        PropertySyncCommand ToSyncCommand();
    }
    
    interface IRpcListPropertySyncMessageImpl: IRpcPropertySyncMessageImpl
    {
        bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo);
        bool MergeKeepOrder(RpcListPropertySyncMessage newMsg);
    }

    public class RpcListPropertyAddSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<RpcPropertyContainer> addInfo_ = new ();
        public List<RpcPropertyContainer> GetAddInfo() => addInfo_; 

        public void AddElem(RpcPropertyContainer elem)
        {
            addInfo_.Add(elem);
        }
        
        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.AddListElem)
            {
                return false;
            }
            
            var addImpl = (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertyAddSyncMessageImpl>();
            var newAddImpl = this;
            addImpl.addInfo_.AddRange(newAddImpl.addInfo_);
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.AddListElem)
            {
                return false;
            }
            var newAddImpl = newMsg.GetImpl<RpcListPropertyAddSyncMessageImpl>();
            addInfo_.AddRange(newAddImpl.addInfo_);
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.AddListElem
            };
            
            foreach (var rpcPropertyContainer in addInfo_)
            {
                cmd.Args.Add(rpcPropertyContainer.ToRpcArg());
            }

            return cmd;
        }
    }

    public class RpcListPropertySetValueSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly Dictionary<int, RpcPropertyContainer> setValueInfo_ = new ();
        Dictionary<int, RpcPropertyContainer> GetSetValueInfo() => setValueInfo_;
        
        public void SetValue(int index, RpcPropertyContainer elem)
        {
            setValueInfo_[index] = elem;
        }
        
        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }
            
            var setValueImpl = (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertySetValueSyncMessageImpl>();
            var newSetValueImpl = this;
            foreach (var kv in newSetValueImpl.setValueInfo_)
            {
                setValueImpl.setValueInfo_[kv.Key] = kv.Value;
            }
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }
            var newSetValueImpl = newMsg.GetImpl<RpcListPropertySetValueSyncMessageImpl>();
            foreach (var kv in newSetValueImpl.setValueInfo_)
            {
                setValueInfo_[kv.Key] = kv.Value;
            }
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.SetValue,
            };

            var dictWithIntKeyArg = new DictWithIntKeyArg();
            foreach (var (key, value) in setValueInfo_)
            {
                dictWithIntKeyArg.PayLoad.Add(key, value.ToRpcArg());
            }
            
            cmd.Args.Add(Any.Pack(dictWithIntKeyArg));
            return cmd;
        }
    }

    public class RpcListPropertyInsertSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<(int, RpcPropertyContainer)> insertInfo_ = new ();
        public List<(int, RpcPropertyContainer)> GetInsertInfo() => insertInfo_;

        public void Insert(int index, RpcPropertyContainer elem)
        {
            insertInfo_.Add((index, elem));
        }
        
        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.InsertElem)
            {
                return false;
            }
            
            var insertImpl = (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertyInsertSyncMessageImpl>();
            var newInsertImpl = this;
            insertImpl.insertInfo_.AddRange(newInsertImpl.insertInfo_);
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.InsertElem)
            {
                return false;
            }
            var newRemoveImpl = newMsg.GetImpl<RpcListPropertyInsertSyncMessageImpl>();
            insertInfo_.AddRange(newRemoveImpl.insertInfo_);
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.InsertElem
            };
            
            foreach (var (index, rpcPropertyContainer) in insertInfo_)
            {
                cmd.Args.Add(Any.Pack(new PairWithIntKey
                {
                    Key = index,
                    Value = rpcPropertyContainer.ToRpcArg()
                }));
            }

            return cmd;
        }
    }

    public class RpcListPropertyRemoveElemMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<int> removeElemInfo_ = new();
        public List<int> GetRemoveElemInfo() => removeElemInfo_;

        public void RemoveElem(int index)
        {
            removeElemInfo_.Add(index);
        }
        
        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.RemoveElem)
            {
                return false;
            }
            
            var removeImpl = (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertyRemoveElemMessageImpl>();
            var newRemoveImpl = this;
            removeImpl.removeElemInfo_.AddRange(newRemoveImpl.removeElemInfo_);
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.RemoveElem)
            {
                return false;
            }
            var newRemoveImpl = newMsg.GetImpl<RpcListPropertyRemoveElemMessageImpl>();
            removeElemInfo_.AddRange(newRemoveImpl.removeElemInfo_);
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.RemoveElem
            };
            
            foreach (var index in removeElemInfo_)
            {
                cmd.Args.Add(Any.Pack(new IntArg
                {
                    PayLoad = index,
                }));
            }

            return cmd;
        }
    }
    
    public class RpcListPropertySyncMessage : RpcPropertySyncMessage
    {
        private readonly IRpcListPropertySyncMessageImpl? impl_;

        public delegate void ListOperation(params object[] args);

        public ListOperation? Action { get; }

        public RpcListPropertySyncMessage(
            MailBox mailbox,
            RpcPropertySyncOperation operation,
            string rpcPropertyPath) 
            : base(mailbox, operation, rpcPropertyPath, RpcSyncPropertyType.List)
        {
            switch (operation)
            {
                case RpcPropertySyncOperation.AddListElem:
                    impl_ = new RpcListPropertyAddSyncMessageImpl();
                    this.Action = args =>
                    {
                        var elem = args[0] as RpcPropertyContainer;
                        if (elem == null)
                        {
                            throw new Exception($"Invalid args {args}");
                        }
                        ((RpcListPropertyAddSyncMessageImpl)impl_).AddElem(elem);
                    };
                    break;
                case RpcPropertySyncOperation.RemoveElem:
                    impl_ = new RpcListPropertyRemoveElemMessageImpl();
                    this.Action = args =>
                    {
                        var index = (int) args[0];
                        ((RpcListPropertyRemoveElemMessageImpl)impl_).RemoveElem(index);
                    };
                    break;
                case RpcPropertySyncOperation.Clear:
                    impl_ = null;
                    this.Action = null;
                    break;
                case RpcPropertySyncOperation.InsertElem:
                    impl_ = new RpcListPropertyInsertSyncMessageImpl();
                    this.Action = args =>
                    {
                        var index = (int) args[0];
                        var elem = args[1] as RpcPropertyContainer;
                        if (elem == null)
                        {
                            throw new Exception($"Invalid args {args}");
                        }
                        ((RpcListPropertyInsertSyncMessageImpl)impl_).Insert(index, elem);
                    };
                    break;
                case RpcPropertySyncOperation.SetValue:
                    impl_ = new RpcListPropertySetValueSyncMessageImpl();
                    this.Action = args =>
                    {
                        var index = (int) args[0];
                        var elem = args[1] as RpcPropertyContainer;
                        if (elem == null)
                        {
                            throw new Exception($"Invalid args {args}");
                        }
                        ((RpcListPropertySetValueSyncMessageImpl)impl_).SetValue(index, elem);
                    };
                    break;
                case RpcPropertySyncOperation.UpdateDict:
                    throw new Exception($"Invalid operation type {operation} for rpc list type.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
            => impl_!.MergeKeepOrder((otherMsg as RpcListPropertySyncMessage)!);

        public override void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo)
        {
            var rpcListPropertySyncInfo = (rpcPropertySyncInfo as RpcListPropertySyncInfo)!;
            if (this.Operation == RpcPropertySyncOperation.Clear)
            {
                rpcListPropertySyncInfo.Clear();
                rpcListPropertySyncInfo.Enque(this);
            }
            else
            {
                var mergeRes = impl_!.MergeIntoSyncInfo(rpcListPropertySyncInfo);
                if (!mergeRes)
                {
                    rpcListPropertySyncInfo.Enque(this);
                }
            }
        }

        public T GetImpl<T>() => (T)impl_!;

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
