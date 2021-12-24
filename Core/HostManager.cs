using LPS.Core.RPC;

namespace LPS.Core
{
    public class HostManager
    {
        public MailBox[] Gates { get; private set; }
        public MailBox[] Servers { get; private set; }
    }
}
