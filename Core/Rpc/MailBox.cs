namespace LPS.Core.Rpc
{
    public class MailBox
    {
        public readonly string ID;
        public string IP { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        public MailBox(string id, string ip, int port, int hostnum)
        {
            this.ID = id;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;
        }

        public override string ToString()
        {
            return $"Mailbox: {this.ID} {this.IP} {this.Port} {this.HostNum}";
        }
        
    }
}
