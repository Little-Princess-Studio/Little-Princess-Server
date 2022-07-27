using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc.RpcPropertySync
{
    public enum RpcSyncPropertyType
    {
        Plaint = 0,
        List = 1,
        Dict = 2,
    }
    
    public abstract class RpcPropertySyncInfo
    {
        private readonly LinkedList<RpcPropertySyncMessage> propPath2SyncMsgQueue_ = new();
        public LinkedList<RpcPropertySyncMessage> PropPath2SyncMsgQueue => propPath2SyncMsgQueue_;
        public abstract Package ToSyncPackage();
        public abstract void AddNewSyncMessage(RpcPropertySyncMessage msg);

        public void Reset()
        {
            PropPath2SyncMsgQueue.Clear();
        }

        public void Enque(RpcPropertySyncMessage msg)
        {
            propPath2SyncMsgQueue_.AddLast(msg);
        }

        public RpcPropertySyncMessage? GetLastMsg()
            => propPath2SyncMsgQueue_.Count > 0 ? propPath2SyncMsgQueue_.Last() : null;
        
        public void PopLastMsg()
        {
            if (propPath2SyncMsgQueue_.Count > 0)
            {
                propPath2SyncMsgQueue_.RemoveLast();
            }
        }
        
        public void Clear() => propPath2SyncMsgQueue_.Clear();
    }

    public class RpcListPropertySyncInfo : RpcPropertySyncInfo
    {
        public override Package ToSyncPackage()
        {
            throw new NotImplementedException();
        }

        public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
        {
            var newMsg = (msg as RpcListPropertySyncMessage)!;
            newMsg.MergeIntoSyncInfo(this);
        }
    }

    public class RpcDictPropertySyncInfo : RpcPropertySyncInfo
    {
        public override Package ToSyncPackage()
        {
            throw new NotImplementedException();
        }

        public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
        {
            var newMsg = (msg as RpcDictPropertySyncMessage)!;
            newMsg.MergeIntoSyncInfo(this);
        }
    }

    public class RpcPlaintPropertySyncInfo : RpcPropertySyncInfo
    {
        public override Package ToSyncPackage()
        {
            throw new NotImplementedException();
        }

        public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
        {
            var newMsg = (msg as RpcPlaintAndCostumePropertySyncMessage)!;
            newMsg.MergeIntoSyncInfo(this);
        }
    }
}