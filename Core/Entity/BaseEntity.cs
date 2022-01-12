using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using MailBox = LPS.Core.Rpc.MailBox;

namespace LPS.Core.Entity
{
    public abstract class BaseEntity {
        public MailBox? MailBox { get; protected init; }

        private readonly Dictionary<uint, (Action<object>, Type)> RpcDict = new ();
        private readonly Dictionary<uint, Action> RpcBlankDict = new ();
        
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
        
        public void SendWithRpcID(uint rpcID, MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var rpcMsg = RpcHelper.BuildRpcMessage(rpcID, rpcMethodName,  this.MailBox!, targetMailBox, args);
            send_.Invoke(rpcMsg);
        }
        
        public Task Call(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName,  this.MailBox!, targetMailBox, args);
            
            var source = new TaskCompletionSource();
            RpcBlankDict[id] = () => source.TrySetResult();

            send_.Invoke(rpcMsg);

            return source.Task;
        }

        public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName,  this.MailBox!, targetMailBox, args);
            
            var source = new TaskCompletionSource<T>();

            RpcDict[id] = (res => source.TrySetResult((T)res), typeof(T)); 
            send_.Invoke(rpcMsg);

            return source.Task;
        }

        // OnResult is a special rpc method with special parameter
        [RpcMethod(Authority.All)]
        public void OnResult(EntityRpc entityRpc)
        {
            var rpcID = entityRpc.RpcID;
            RpcAsyncCallBack(rpcID, entityRpc);
        }

        private void RpcAsyncCallBack(uint rpcID, EntityRpc entityRpc)
        {
            if (RpcDict.ContainsKey(rpcID))
            {
                var (callback, returnType) = RpcDict[rpcID];
                var rpcArg = RpcHelper.ProtobufToRpcArg(entityRpc.Args[0], returnType);
                callback.Invoke(rpcArg!);
            }
            else
            {
                var callback = RpcBlankDict[rpcID];
                callback.Invoke();
            }
        }
    }
}
