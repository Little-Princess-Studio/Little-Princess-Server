using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Common.Core.Rpc.RpcProperty.RpcContainer;

namespace LPS.Common.Core.Rpc.RpcPropertySync
{
    public class RpcPlaintAndCostumePropertySyncMessage : RpcPropertySyncMessage
    {
        public RpcPropertyContainer Val;

        public RpcPlaintAndCostumePropertySyncMessage(MailBox mailbox, 
            RpcPropertySyncOperation operation,
            string rpcPropertyPath,
            RpcPropertyContainer val) 
            : base(mailbox, operation, rpcPropertyPath, RpcSyncPropertyType.PlaintAndCostume)
        {
            this.Val = val;
        }

        public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
        {
            if (otherMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            this.Val = (otherMsg as RpcPlaintAndCostumePropertySyncMessage)!.Val;
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
                (lastMsg as RpcPlaintAndCostumePropertySyncMessage)!.Val = this.Val;
            }
        }

        public override PropertySyncCommand Serialize()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.SetValue,
            };

            cmd.Args.Add(this.Val.ToRpcArg());
            
            return cmd;
        }
    }
}
