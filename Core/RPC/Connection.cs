using System.Net;
using LPS.Core.IPC;
using LPS.Core.RPC.Protocal;

namespace LPS.Core.RPC
{
    public enum Status
    {
        Init = 0,
        Connected,
        Reconnect,
        DisConnected,
    }

    public class Connection
    {
        public IProtocal Protocal { get; private set; }
        public Status Status { get; private set; }

        public MailBox RemoteMailBox { get; private set; }

        private Connection() { }

        public static Connection Create(MailBox remoteMailBox)
        {
            var newConnection = new Connection
            {
                RemoteMailBox = remoteMailBox,
            };

            return newConnection;
        }

        public void ReConnect() { }

        public void SendData() { }

        public void OnDataRecieved() { }
    }
}
