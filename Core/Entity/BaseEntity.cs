using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using MailBox = LPS.Core.Rpc.MailBox;

namespace LPS.Core.Entity
{
    public abstract class BaseEntity {
        public MailBox? MailBox { get; protected init; }

        private static readonly Dictionary<uint, Action<object>> RpcDict = new ();

        private readonly Action<EntityRpc> send_;

        private uint rpcID_;

        protected BaseEntity(Action<EntityRpc> send)
        {
            send_ = send;
        }

        public void Send(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName,  this.MailBox!, targetMailBox, args);
            send_.Invoke(rpcMsg);
        }
        
        public Task<object> Call(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName,  this.MailBox!, targetMailBox, args);
            
            var source = new TaskCompletionSource<object>();

            RpcDict[id] = CallBack; 
            send_.Invoke(rpcMsg);

            return source.Task;
            
            void CallBack(object res)
            {
                source.TrySetResult(res);
            }
        }

        public void RpcAsyncCallBack(uint rpcID, object res)
        {
            RpcDict[rpcID].Invoke(res);
        }
    }
}
