using LPS.Core.Debug;
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

        public bool IsDestroyed { get; protected set; }
        
        // if an entity is frozen, it can only send rpc to client
        public bool IsFrozen { get; protected set; }
        
        public Action<EntityRpc> OnSend { get; set; } = null!;

        private uint rpcId_;

        public string Serialize()
        {
            return "";
        }

        public void Deserialize(string content)
        {
            
        }

        public void Destroy()
        {
            this.IsDestroyed = true;
        }
        
        public void Send(MailBox targetMailBox, string rpcMethodName, bool notifyOnly, RpcType rpcType, params object?[] args)
        {
            if (this.IsDestroyed)
            {
                throw new Exception("Entity already destroyed.");
            }

            if (this.IsFrozen && rpcType != RpcType.ServerToClient)
            {
                throw new Exception("Entity is frozen.");
            }
            
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox,
                notifyOnly, rpcType, args);
            OnSend.Invoke(rpcMsg);
        }

        public void SendWithRpcId(uint rpcId, MailBox targetMailBox, string rpcMethodName, bool notifyOnly, RpcType rpcType,
            params object?[] args)
        {
            if (this.IsDestroyed)
            {
                throw new Exception("Entity already destroyed.");
            }
            
            if (this.IsFrozen && rpcType != RpcType.ServerToClient)
            {
                throw new Exception("Entity is frozen.");
            }
            
            var rpcMsg = RpcHelper.BuildRpcMessage(
                rpcId, rpcMethodName, this.MailBox, targetMailBox,
                notifyOnly, rpcType, args);
            OnSend.Invoke(rpcMsg);
        }

        // BaseEntity.Call/Call<T> will return a promise 
        // which always wait for remote git a callback and give caller a async result.
        public Task Call(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
        {
            if (this.IsDestroyed)
            {
                throw new Exception("Entity already destroyed.");
            }
            
            if (this.IsFrozen && rpcType != RpcType.ServerToClient)
            {
                throw new Exception("Entity is frozen.");
            }
            
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);

            var cancellationTokenSource = new CancellationTokenSource(5000);
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

        public Task Call(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
            this.Call(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

        public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
        {
            if (this.IsDestroyed)
            {
                throw new Exception("Entity already destroyed.");
            }
            
            if (this.IsFrozen && rpcType != RpcType.ServerToClient)
            {
                throw new Exception("Entity is frozen.");
            }
            
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox, false, rpcType, args);


            var cancellationTokenSource = new CancellationTokenSource(5000);
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
        
        public Task<T> Call<T>(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
            this.Call<T>(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

        // BaseEntity.Notify will not return any promise and only send rpc message to remote
        public void Notify(MailBox targetMailBox, string rpcMethodName, RpcType rpcType, params object?[] args)
        {
            if (this.IsDestroyed)
            {
                throw new Exception("Entity already destroyed.");
            }
            
            if (this.IsFrozen && rpcType != RpcType.ServerToClient)
            {
                throw new Exception("Entity is frozen.");
            }
            
            var id = rpcId_++;
            var rpcMsg = RpcHelper.BuildRpcMessage(
                id, rpcMethodName, this.MailBox, targetMailBox, true, rpcType, args);
            OnSend.Invoke(rpcMsg);
        }

        public void Notify(MailBox targetMailBox, string rpcMethodName, params object?[] args) =>
            this.Notify(targetMailBox, rpcMethodName, RpcType.ServerInside, args);

        // OnResult is a special rpc method with special parameter
        [RpcMethod(Authority.All)]
        public void OnResult(EntityRpc entityRpc)
        {
            if (this.IsDestroyed)
            {
                Logger.Warn("Entity already destroyed.");
                return;
            }
            
            var rpcId = entityRpc.RpcID;
            RpcAsyncCallBack(rpcId, entityRpc);
        }

        private void RpcAsyncCallBack(uint rpcId, EntityRpc entityRpc)
        {
            Logger.Debug($"[RpcAsyncCallBack] {entityRpc}");
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

        ~BaseEntity()
        {
            Logger.Info("Entity destroyed");
        }
    }
}