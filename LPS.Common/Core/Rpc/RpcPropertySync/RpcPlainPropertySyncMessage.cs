using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;
using LPS.Core.Rpc.RpcPropertySync;

namespace LPS.Core.Ipc.SyncMessage
{
    public class RpcPlainPropertySyncMessage : RpcPropertySyncMessage
    {
        public RpcPropertyContainer Val;

        public RpcPlainPropertySyncMessage(MailBox mailbox, 
            RpcPropertySyncOperation operation,
            string rpcPropertyPath,
            RpcSyncPropertyType rpcSyncPropertyType) 
            : base(mailbox, operation, rpcPropertyPath, rpcSyncPropertyType)
        {
        }

        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
        {
            if (otherMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            this.Val = (otherMsg as RpcPlainPropertySyncMessage)!.Val;
            return true;
        }

        public override void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo)
        {
            var lastMsg = rpcPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                rpcPropertySyncInfo.Enque(this);
            }
            else
            {
                (lastMsg as RpcPlainPropertySyncMessage)!.Val = this.Val;
            }
        }

        public override byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
