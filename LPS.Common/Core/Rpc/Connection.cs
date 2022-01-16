using System.Net.Sockets;

namespace LPS.Core.Rpc
{
    public enum ConnectStatus
    {
        Init = 0,
        Connected,
        Reconnect,
        DisConnected,
    }

    public class Connection
    {
        public ConnectStatus Status { get; private set; }
        public Socket Socket { get; private init; } = null!;
        public CancellationTokenSource TokenSource { get; private init; } = null!;
        public MailBox MailBox { get; set; }

        public uint ConnectionID { get; set; } = uint.MaxValue;
        
        private Connection() { }
        public static Connection Create(Socket socket, CancellationTokenSource tokenSource)
        {
            var newConnection = new Connection
            {
                Status = ConnectStatus.Init,
                Socket = socket,
                TokenSource = tokenSource,
            };
            return newConnection;
        }

        public void Connect()
        {
            this.Status = ConnectStatus.Connected;
        }


        public void DisConnect()
        {
            this.Status = ConnectStatus.DisConnected;
        }
    }
}
