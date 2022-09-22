using LPS.Common.Core.Debug;
using LPS.Common.Core.Entity;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.InnerMessages;
using MailBox = LPS.Common.Core.Rpc.MailBox;

namespace LPS.Client.Core.Entity
{
    public class ServerProxy
    {
        public readonly MailBox MailBox;
        public readonly BaseEntity ClientOwner;

        public ServerProxy(MailBox mailBox, BaseEntity clientOwner)
        {
            this.MailBox = mailBox;
            this.ClientOwner = clientOwner;
        }
        
        public Task<T> Call<T>(string methodName, params object?[] args)
        {
            return this.ClientOwner.Call<T>(MailBox, methodName, RpcType.ClientToServer, args);
        }

        public Task Call(string methodName, params object?[] args)
        {
            return this.ClientOwner.Call(MailBox, methodName, RpcType.ClientToServer, args);
        }

        public void Notify(string methodName, params object?[] args)
        {
            this.ClientOwner.Notify(MailBox, methodName, RpcType.ClientToServer, args);
        }
    }
    
    [EntityClass]
    public class ShadowClientEntity : ShadowEntity
    {
        public ServerProxy Server { get; private set; } = null!;

        public void BindServerMailBox()
        {
            this.Server = new ServerProxy(this.MailBox, this);
        }

        [RpcMethod(Authority.ClientStub)]
        public ValueTask OnTransfer(MailBox newMailBox)
        {
            
            Logger.Debug($"entity transferred {newMailBox}");
            
            this.MailBox = newMailBox;
            this.Server = new ServerProxy(this.MailBox, this);
            return default;
        }
    }
}
