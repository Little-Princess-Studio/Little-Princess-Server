using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    [EntityClass]
    public class DistributeEntity : BaseEntity
    {
        public MailBox GateMailBox { get; set; }

        protected DistributeEntity(string desc)
        {

        }
    }
}
