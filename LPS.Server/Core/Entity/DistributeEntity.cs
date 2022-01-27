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

        /*
         * Steps for entity transfer to other server
         *
         * 1. set entity status to frozen
         * 2. serialize entity
         * 3. send request rpc to target cell and wait
         * 4. remote cell creates a clone entity locally
         * 5. remote clone entity rebuild
         * 6. remote cell add new entity
         * 7. remote server bind gate connection to created new entity
         * 8. origin entity notify client change mailbox (if entity is ServerClientEntity)
         * 9. destroy origin entity
         */
        public virtual async Task TransferIntoCell(MailBox targetCellMailBox, string transferInfo)
        {
            if (targetCellMailBox.CompareFull(this.Cell.MailBox))
            {
                return;
            }
            
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
            
            this.Destroy();
        }

        public void OnTransferred(string transferInfo)
        {
        }
    }
}
