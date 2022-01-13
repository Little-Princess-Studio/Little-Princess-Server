using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using MailBox = LPS.Core.Rpc.MailBox;

namespace LPS.Core.Entity
{
    public class RpcTimeOutException : Exception
    {
        public readonly BaseEntity Who;
        public readonly uint RpcID;

        public RpcTimeOutException(BaseEntity who, uint rpcID) : base("Rpc time out.")
        {
            this.Who = who;
            this.RpcID = rpcID;
        }
    }

    public abstract class BaseEntity
    {
        public MailBox? MailBox { get; set; }

        private readonly Dictionary<uint, (Action<object>, Type)> RpcDict = new();
        private readonly Dictionary<uint, Action> RpcBlankDict = new();

        private Action<EntityRpc>? send_;

        public Action<EntityRpc> OnSend
        {
            private get => send_!;
            set => send_ = value;
        }

        private uint rpcID_;

        public void Send(MailBox targetMailBox, string rpcMethodName, bool notifyOnly, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName, this.MailBox!, targetMailBox, notifyOnly, args);
            OnSend.Invoke(rpcMsg);
        }

        public void SendWithRpcID(uint rpcID, MailBox targetMailBox, string rpcMethodName, bool notifyOnly, params object?[] args)
        {
            var rpcMsg = RpcHelper.BuildRpcMessage(rpcID, rpcMethodName, this.MailBox!, targetMailBox, notifyOnly, args);
            OnSend.Invoke(rpcMsg);
        }

        // BaseEntity.Call/Call<T> will return a promise 
        // which always wait for remote git a callback and give caller a async result.
        public Task Call(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName, this.MailBox!, targetMailBox, false, args);

            var cancellationTokenSource = new CancellationTokenSource(1000);
            var source = new TaskCompletionSource();

            cancellationTokenSource.Token.Register(
                () =>
                {
                    this.RemoveRpcRecord(id);
                    source.TrySetException(new RpcTimeOutException(this, id));
                }, false);

            RpcBlankDict[id] = () => source.TrySetResult();
            OnSend.Invoke(rpcMsg);

            return source.Task;
        }

        public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName, this.MailBox!, targetMailBox, false, args);


            var cancellationTokenSource = new CancellationTokenSource(1000);
            var source = new TaskCompletionSource<T>();

            cancellationTokenSource.Token.Register(
                () =>
                {
                    this.RemoveRpcRecord(id);
                    source.TrySetException(new RpcTimeOutException(this, id));
                }, false);

            RpcDict[id] = (res => source.TrySetResult((T)res), typeof(T));
            OnSend.Invoke(rpcMsg);

            return source.Task;
        }

        // BaseEntity.Notify will not return any promise and only send rpc message to remote
        public void Notify(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcID_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(id, rpcMethodName, this.MailBox!, targetMailBox, true, args);
            OnSend.Invoke(rpcMsg);
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
                RpcDict.Remove(rpcID);
            }
            else
            {
                var callback = RpcBlankDict[rpcID];
                callback.Invoke();
                RpcBlankDict.Remove(rpcID);
            }
        }

        public void RemoveRpcRecord(uint rpcID)
        {
            if (RpcDict.ContainsKey(rpcID))
            {
                RpcDict.Remove(rpcID);
            }
            else
            {
                RpcBlankDict.Remove(rpcID);
            }
        }
    }
}
