using LPS.Common.Core.Rpc;
using LPS.Server.Core.Rpc;

namespace LPS.Server.Core
{
    public class HostManager
    {
        public MailBox[] Gates { get; private set; }
        public MailBox[] Servers { get; private set; }
    }
}
