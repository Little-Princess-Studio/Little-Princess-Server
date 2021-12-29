using System.Threading.Tasks;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    abstract public class BaseEntity {
        public MailBox MailBox { get; private set; }

        public BaseEntity()
        {
        }

        public void RegisterRpcMethods()
        {

        }

        public async Task<object> Call(MailBox targetMailBox, object[] args)
        {
            return null;
        }

        public async Task<object[]> CallMultiple(MailBox[] targetMailBox, object[] args)
        {
            return null;
        }
    }
}
