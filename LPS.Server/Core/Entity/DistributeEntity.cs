using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    [EntityClass]
    public class DistributeEntity : BaseEntity
    {
        public Rpc.MailBox? GateMailBox { get; set; }

        public DistributeEntity(string desc)
        {

        }
    }
}
