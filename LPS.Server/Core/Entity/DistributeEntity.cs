using System;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    [EntityClass]
    public abstract class DistributeEntity : BaseEntity
    {
        public CellEntity Cell { get; private set; } = null!;
        
        protected DistributeEntity(string desc)
        {
        }

        protected DistributeEntity()
        {
        }

        public virtual async Task TransferIntoCell(MailBox targetCellMailBox, string transferInfo)
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

        public void OnTransferred(string transferInfo)
        {
        }
    }
}
