using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcPropertySync;

// operation on List is very complex, so RpcListPropertySyncInfo
// will only merge same operation for a continuous operation sequence

namespace LPS.Core.Ipc.SyncMessage
{
    interface IRpcListPropertySyncMessageImpl
    {
        bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo);
        bool MergeKeepOrder(RpcListPropertySyncMessage newMsg);
    }

    public class RpcListPropertyAddSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<object> addInfo_ = new ();

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
    }

    public class RpcListPropertySetValueSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly Dictionary<int, object> setValueInfo_ = new ();
        
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
    }

    public class RpcListPropertyInsertSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<(int, object)> insertInfo_ = new ();
        
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
    }

    public class RpcListPropertyRemoveElemMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<int> removeElemInfo_ = new();
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
    }
    
    public class RpcListPropertySyncMessage : RpcPropertySyncMessage
    {
        private readonly IRpcListPropertySyncMessageImpl impl_;

        public RpcListPropertySyncMessage(
            MailBox mailbox,
            RpcPropertySyncOperation operation,
            string rpcPropertyPath, 
            RpcSyncPropertyType rpcSyncPropertyType) 
            : base(mailbox, operation, rpcPropertyPath, rpcSyncPropertyType)
        {
            switch (operation)
            {
                case RpcPropertySyncOperation.AddListElem:
                    impl_ = new RpcListPropertyAddSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.RemoveElem:
                    impl_ = new RpcListPropertyRemoveElemMessageImpl();
                    break;
                case RpcPropertySyncOperation.Clear:
                    break;
                case RpcPropertySyncOperation.InsertElem:
                    impl_ = new RpcListPropertyInsertSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.SetValue:
                    impl_ = new RpcListPropertySetValueSyncMessageImpl();
                    break;
                case RpcPropertySyncOperation.UpdateDict:
                    throw new Exception($"Invalid operation type {operation} for rpc list type.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }
        

        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
            => impl_.MergeKeepOrder((otherMsg as RpcListPropertySyncMessage)!);

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
                var mergeRes = impl_.MergeIntoSyncInfo(rpcListPropertySyncInfo);
                if (!mergeRes)
                {
                    rpcListPropertySyncInfo.Enque(this);
                }
            }
        }

        public T GetImpl<T>() => (T)impl_;

        public override byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
