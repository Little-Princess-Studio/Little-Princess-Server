namespace LPS.Core.Rpc
{
    public class MailBox
    {
        public string IP { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }
        public int LocalThreadID { get; private set; }

        public MailBox(string ip, int port, int hostnum, int localThreadID)
        {
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;
            this.LocalThreadID = localThreadID;
        }
    }
}
