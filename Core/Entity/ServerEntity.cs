using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    internal class ServerEntity : UniqueEntity
    {
        public ServerEntity(MailBox mailbox)
        {
            this.MailBox = mailbox;
        }

        [RpcMethod(Authority.All)]
        public void Echo()
        {
            Logger.Info("Echo Echo Echo");
        }
    }
}
