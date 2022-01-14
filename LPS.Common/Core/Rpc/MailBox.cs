namespace LPS.Core.Rpc
{
    public struct MailBox
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

        public bool CompareOnlyID(MailBox other)
        {
            return this.ID == other.ID;
        }

        public bool CompareOnlyID(InnerMessages.MailBox other)
        {
            return this.ID == other.ID;
        }

        public bool CompareFull(MailBox other)
        {
            return this.ID == other.ID
                && this.IP == other.IP
                && this.Port == other.Port
                && this.HostNum == other.HostNum;
        }

        public bool CompareFull(InnerMessages.MailBox other)
        {
            return this.ID == other.ID
                && this.IP == other.IP
                && this.Port == other.Port
                && this.HostNum == other.HostNum;
        }

        public bool CompareOnlyAddress(InnerMessages.MailBox other)
        {
            return this.IP == other.IP && this.Port == other.Port;
        }

        public bool CompareOnlyAddress(MailBox other)
        {
            return this.IP == other.IP && this.Port == other.Port;
        }

    }
}
