using System.Threading.Tasks;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    abstract public class BaseEntity {
        public MailBox MailBox { get; protected set; }

        public BaseEntity()
        {
        }

        public async Task<object> Call(MailBox targetMailBox, params object[] args)
        {
            return null;
        }

        public async Task<object[]> CallMultiple(MailBox[] targetMailBox, params object[] args)
        {
            return null;
        }
    }
}
