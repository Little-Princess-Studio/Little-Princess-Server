using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;
using LPS.Core.Rpc.RpcPropertySync;

namespace LPS.Core.Ipc.SyncMessage
{
    public class RpcDictPropertySyncMessage : RpcPropertySyncMessage
    {
        private Dictionary<object, RpcPropertyContainer>? updateDictInfo_;
        private HashSet<object>? removeDictInfo_;
        
        public delegate void DictOperation(params object[] args);

        public DictOperation? Action { get; }

        public RpcDictPropertySyncMessage(MailBox mailbox, 
            RpcPropertySyncOperation operation,
            string rpcPropertyPath,
            RpcSyncPropertyType rpcSyncPropertyType) 
            : base(mailbox, operation, rpcPropertyPath, rpcSyncPropertyType)
        {
            this.Action = operation switch
            {
                RpcPropertySyncOperation.UpdateDict => args =>
                {
                    var key = args[0];
                    var val = args[1] as RpcPropertyContainer;

                    updateDictInfo_ ??= new();
                    updateDictInfo_[key] = val ?? throw new Exception($"Invalid args {args}");
                },
                RpcPropertySyncOperation.RemoveElem => args =>
                {
                    var key = args[0];

                    removeDictInfo_ ??= new();
                    removeDictInfo_.Add(key);
                },
                _ => throw new Exception($"Invalid operation {operation}")
            };
        }
        
        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
        {
            if (this.Operation != otherMsg.Operation)
            {
                return false;
            }

            var msg = (otherMsg as RpcDictPropertySyncMessage)!;
            
            switch (this.Operation)
            {
                case RpcPropertySyncOperation.SetValue:
                {
                    foreach (var (key, value) in msg.updateDictInfo_!)
                    {
                        updateDictInfo_![key] = value;
                    }
                    break;
                }
                case RpcPropertySyncOperation.RemoveElem:
                {
                    foreach (var key in msg.removeDictInfo_!)
                    {
                        removeDictInfo_!.Add(key);
                    }
                    break;
                }
                default:
                    throw new Exception($"Invalid operation {otherMsg.Operation}");
            }

            return true;
        }

        public override void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo)
        {
            var dictInfo = (rpcPropertySyncInfo as RpcDictPropertySyncInfo)!;

            // if we have got a clear msg,
            // then cancel all the previous modification and record the clear msg
            if (this.Operation == RpcPropertySyncOperation.Clear)
            {
                dictInfo.SyncMsg = null;
                rpcPropertySyncInfo.PropPath2SyncMsgQueue.Clear();
                rpcPropertySyncInfo.Enque(this);
                return;
            }
            
            if (dictInfo.SyncMsg == null)
            {
                dictInfo.SyncMsg = this;
                rpcPropertySyncInfo.Enque(this);
            }
            else
            {
                var curDictSyncInfo = dictInfo.SyncMsg;
                switch (this.Operation)
                {
                    case RpcPropertySyncOperation.UpdateDict:
                        HandleUpdateDict(curDictSyncInfo);
                        break;
                    case RpcPropertySyncOperation.RemoveElem:
                        HandleRemove(curDictSyncInfo);
                        break;
                    default:
                        throw new Exception($"Invalid operation for rpc dict property {this.Operation}");
                }
            }
        }

        private void HandleUpdateDict(RpcDictPropertySyncMessage curDictSyncInfo)
        {
            if (curDictSyncInfo.updateDictInfo_ == null)
            {
                curDictSyncInfo.updateDictInfo_ = updateDictInfo_;
            }
            else
            {
                foreach (var (key, value) in updateDictInfo_!)
                {
                    curDictSyncInfo.updateDictInfo_[key] = value;
                    if (curDictSyncInfo.removeDictInfo_ != null
                        && curDictSyncInfo.removeDictInfo_.Contains(key))
                    {
                        curDictSyncInfo.removeDictInfo_.Remove(value);
                    }
                }
            }
        }

        private void HandleRemove(RpcDictPropertySyncMessage curDictSyncInfo)
        {
            if (curDictSyncInfo.removeDictInfo_ == null)
            {
                curDictSyncInfo.removeDictInfo_ = removeDictInfo_;
            }
            else
            {
                foreach (var key in removeDictInfo_!)
                {
                    curDictSyncInfo.removeDictInfo_.Add(key);
                    if (curDictSyncInfo.updateDictInfo_ != null
                        && curDictSyncInfo.updateDictInfo_.ContainsKey(key))
                    {
                        curDictSyncInfo.updateDictInfo_.Remove(key);
                    }
                }
            }
        }

        public override byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }    
}
