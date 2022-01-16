using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using MailBox = LPS.Core.Rpc.MailBox;

namespace LPS.Core.Entity
{
    public class RpcTimeOutException : Exception
    {
        public readonly BaseEntity Who;
        public readonly uint RpcId;

        public RpcTimeOutException(BaseEntity who, uint rpcId) : base("Rpc time out.")
        {
            this.Who = who;
            this.RpcId = rpcId;
        }
    }

    public abstract class BaseEntity
    {
        public MailBox MailBox { get; set; }

        private readonly Dictionary<uint, (Action<object>, Type)> rpcDict_ = new();
        private readonly Dictionary<uint, Action> rpcBlankDict_ = new();

        public Action<EntityRpc> OnSend { get; set; } = null!;

        private uint rpcId_;

        public void Send(MailBox targetMailBox, string rpcMethodName, bool notifyOnly, params object?[] args)
        {
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox,
                notifyOnly, false, args);
            OnSend.Invoke(rpcMsg);
        }

        public void SendWithRpcId(uint rpcId, MailBox targetMailBox, string rpcMethodName, bool notifyOnly,
            params object?[] args)
        {
            var rpcMsg = RpcHelper.BuildRpcMessage(
                rpcId, rpcMethodName, this.MailBox, targetMailBox,
                notifyOnly, false, args);
            OnSend.Invoke(rpcMsg);
        }

        // BaseEntity.Call/Call<T> will return a promise 
        // which always wait for remote git a callback and give caller a async result.
        public Task Call(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox, false, false, args);

            var cancellationTokenSource = new CancellationTokenSource(1000);
            var source = new TaskCompletionSource();

            cancellationTokenSource.Token.Register(
                () =>
                {
                    this.RemoveRpcRecord(id);
                    source.TrySetException(new RpcTimeOutException(this, id));
                }, false);

            rpcBlankDict_[id] = () => source.TrySetResult();
            OnSend.Invoke(rpcMsg);

            return source.Task;
        }

        public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox, false, false, args);


            var cancellationTokenSource = new CancellationTokenSource(1000);
            var source = new TaskCompletionSource<T>();

            cancellationTokenSource.Token.Register(
                () =>
                {
                    this.RemoveRpcRecord(id);
                    source.TrySetException(new RpcTimeOutException(this, id));
                }, false);

            rpcDict_[id] = (res => source.TrySetResult((T) res), typeof(T));
            OnSend.Invoke(rpcMsg);

            return source.Task;
        }

        // BaseEntity.Notify will not return any promise and only send rpc message to remote
        public void Notify(MailBox targetMailBox, string rpcMethodName, params object?[] args)
        {
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox, true, false, args);
            OnSend.Invoke(rpcMsg);
        }

        // OnResult is a special rpc method with special parameter
        [RpcMethod(Authority.All)]
        public void OnResult(EntityRpc entityRpc)
        {
            var rpcId = entityRpc.RpcID;
            RpcAsyncCallBack(rpcId, entityRpc);
        }

        private void RpcAsyncCallBack(uint rpcId, EntityRpc entityRpc)
        {
            if (rpcDict_.ContainsKey(rpcId))
            {
                var (callback, returnType) = rpcDict_[rpcId];
                var rpcArg = RpcHelper.ProtobufToRpcArg(entityRpc.Args[0], returnType);
                callback.Invoke(rpcArg!);
                rpcDict_.Remove(rpcId);
            }
            else
            {
                var callback = rpcBlankDict_[rpcId];
                callback.Invoke();
                rpcBlankDict_.Remove(rpcId);
            }
        }

        public void RemoveRpcRecord(uint rpcId)
        {
            if (rpcDict_.ContainsKey(rpcId))
            {
                rpcDict_.Remove(rpcId);
            }
            else
            {
                rpcBlankDict_.Remove(rpcId);
            }
        }
    }
}