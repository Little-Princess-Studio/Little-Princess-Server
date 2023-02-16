using LPS.Common.Core.Rpc.InnerMessages;

namespace LPS.Common.Core.Rpc.RpcPropertySync
{
    public enum RpcPropertySyncOperation
    {
        SetValue = 0,
        UpdatePair = 1,
        AddListElem = 2,
        RemoveElem = 3,
        Clear = 4,
        InsertElem = 5,
    }

    public abstract class RpcPropertySyncMessage
    {
        public readonly MailBox MailBox;
        public readonly RpcPropertySyncOperation Operation;
        public readonly string RpcPropertyPath;
        public readonly RpcSyncPropertyType RpcSyncPropertyType;

        public RpcPropertySyncMessage(MailBox mailbox, RpcPropertySyncOperation operation, string rpcPropertyPath, RpcSyncPropertyType rpcSyncPropertyType)
        {
            this.MailBox = mailbox;
            this.Operation = operation;
            this.RpcPropertyPath = rpcPropertyPath;
            this.RpcSyncPropertyType = rpcSyncPropertyType;
        }

        public abstract bool MergeKeepOrder(RpcPropertySyncMessage otherMsg);
        
        public abstract void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo);

        public bool CanMerge(in RpcPropertySyncMessage otherMsg)
        {
            return this.Operation == otherMsg.Operation 
                && MailBox.CompareOnlyID(otherMsg.MailBox);
        }

        public abstract PropertySyncCommand Serialize();
    }

}