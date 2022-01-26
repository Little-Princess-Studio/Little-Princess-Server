using System;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    public class ClientProxy
    {
        // gateConnection_ record which gate the client is connecting to
        private Connection gateConnection_;
        public Connection GateConn => gateConnection_;

        public ClientProxy(Connection gateConnection)
        {
            gateConnection_ = gateConnection;
        }

        public Task<T> Call<T>(string methodName, params object?[] args)
        {
            return null!;
        }

        public Task Call(string methodName, params object?[] args)
        {
            return null!;
        }

        public void Notify(string methodName, params object?[] args)
        {
            
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
                var (res, mailbox) = await this.Call<(bool, MailBox)>(targetCellMailBox,
                    "RequireTransfer",
                    this.MailBox,
                    this.GetType().Name,
                    serialContent,
                    transferInfo);

                if (!res)
                {
                    this.IsFrozen = false;
                    throw new Exception("Error when transfer to cell");
                }

                res = await this.Client.Call<bool>("OnTransfer", mailbox);

                if (!res)
                {
                    this.IsFrozen = false;
                    throw new Exception("Error when notify client transfer");
                }
                
                this.Cell.OnEntityLeave(this);
                
                Logger.Debug($"transfer success, new mailbox {mailbox}");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when transfer to cell");
                throw;
            }
            
            this.IsDestroied = true;
        }

        public void BindGateConn(Connection gateConnection)
        {
            this.Client = new ClientProxy(gateConnection);
        }
    }
}
