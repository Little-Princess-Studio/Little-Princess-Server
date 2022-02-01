using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;

namespace LPS.Core.Ipc
{

    public class RpcPropertySyncInfo
    {
        private Queue<RpcPropertySyncMessage> operationQueue_ = new();
    }

    public enum RpcPropertySyncOperation
    {
        SetValue = 0,
        AddElems = 1,
        AddPairs = 2,
        RemoveElem = 3,
        RemovePairs = 4,
        Clear = 5,
    }

    public struct RpcPropertySyncMessage
    {
        public readonly MailBox MailBox;
        public readonly RpcPropertySyncOperation Operation;
        public readonly string RpcPropertyPath;
        public List<object> Values;

        private bool CanMerge(in RpcPropertySyncMessage otherMsg)
        {
            return this.Operation == otherMsg.Operation 
                && MailBox.CompareFull(otherMsg.MailBox);
        }

        public bool Merge(in RpcPropertySyncMessage otherMsg)
        {   
            if (!CanMerge(otherMsg))
            {
                return false;
            }

            switch (Operation)
            {
                case RpcPropertySyncOperation.Clear:
                    break;
                case RpcPropertySyncOperation.SetValue:
                    this.Values = otherMsg.Values;
                    break;
                case RpcPropertySyncOperation.AddElems:
                case RpcPropertySyncOperation.AddPairs:
                case RpcPropertySyncOperation.RemoveElem:
                case RpcPropertySyncOperation.RemovePairs:
                    this.Values.AddRange(otherMsg.Values);
                    break;
                default:
                    break;
            }            

            return true;
        }

        public byte[] Serialize()
        {
            return default;
        }
    }
}