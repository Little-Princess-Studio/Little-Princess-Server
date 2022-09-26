namespace LPS.Common.Core.Rpc
{
    public struct MailBox
    {
        public readonly string Id;
        public string Ip { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        public MailBox(string id, string ip, int port, int hostNum)
        {
            this.Id = id;
            this.Ip = ip;
            this.Port = port;
            this.HostNum = hostNum;
        }
        
        public MailBox(): this("", "", 0, 0) {}

        public override string ToString()
        {
            return $"{this.Id} {this.Ip} {this.Port} {this.HostNum}";
        }

        public bool CompareOnlyID(MailBox other)
        {
            return this.Id == other.Id;
        }

        public bool CompareOnlyID(InnerMessages.MailBox other)
        {
            return this.Id == other.ID;
        }

        public readonly bool CompareFull(MailBox other)
        {
            return this.Id == other.Id
                && this.Ip == other.Ip
                && this.Port == other.Port
                && this.HostNum == other.HostNum;
        }

        public bool CompareFull(InnerMessages.MailBox other)
        {
            return this.Id == other.ID
                && this.Ip == other.IP
                && this.Port == other.Port
                && this.HostNum == other.HostNum;
        }

        public bool CompareOnlyAddress(InnerMessages.MailBox other)
        {
            return this.Ip == other.IP && this.Port == other.Port;
        }

        public bool CompareOnlyAddress(MailBox other)
        {
            return this.Ip == other.Ip && this.Port == other.Port;
        }

    }
}
