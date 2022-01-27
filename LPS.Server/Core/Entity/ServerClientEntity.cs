using System;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using MailBox = LPS.Core.Rpc.MailBox;

namespace LPS.Core.Entity
{
    public class ClientProxy
    {
        // gateConnection_ record which gate the client is connecting to
        public readonly ServerClientEntity Owner;
        private Connection gateConnection_;
        public Connection GateConn => gateConnection_;

        public ClientProxy(Connection gateConnection, ServerClientEntity owner)
        {
            gateConnection_ = gateConnection;
            Owner = owner;
        }

        public Task<T> Call<T>(string methodName, params object?[] args)
        {
            return this.Owner.Call<T>(this.Owner.MailBox, methodName, RpcType.ServerToClient, args);
        }

        public Task Call(string methodName, params object?[] args)
        {
            return this.Owner.Call(this.Owner.MailBox, methodName, RpcType.ServerToClient, args);
        }

        public void Notify(string methodName, params object?[] args)
        {
            this.Owner.Notify(this.Owner.MailBox, methodName, RpcType.ServerInside, args);
        }
    }

    [EntityClass]
    public class ServerClientEntity : DistributeEntity
    {
        public ClientProxy Client { get; private set; } = null!;

        protected ServerClientEntity(string desc): base(desc)
        {
        }

        protected ServerClientEntity()
        {
        }

        public override async Task TransferIntoCell(MailBox targetCellMailBox, string transferInfo)
        {
            this.IsFrozen = true;

            //todo: serialContent is the serialized rpc property tree of entity
            
            Logger.Debug($"start transfer to {targetCellMailBox}");
            
            var serialContent = "";
            try
            {
                var gateMailBox = this.Client.GateConn.MailBox;
                
                var (res, mailbox) = await this.Call<(bool, MailBox)>(targetCellMailBox,
                    "RequireTransfer",
                    this.MailBox,
                    this.GetType().Name,
                    serialContent,
                    transferInfo,
                    gateMailBox);

                if (!res)
                {
                    this.IsFrozen = false;
                    throw new Exception("Error when transfer to cell");
                }

                this.Client.Notify("OnTransfer", mailbox);
                this.Cell.OnEntityLeave(this);
                
                Logger.Debug($"transfer success, new mailbox {mailbox}");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when transfer to cell");
                throw;
            }
            
            this.IsDestroyed = true;
        }

        public void BindGateConn(Connection gateConnection)
        {
            this.Client = new ClientProxy(gateConnection, this);
        }
    }
}
