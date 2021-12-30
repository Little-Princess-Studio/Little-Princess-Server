using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
        public Socket Socket { get; private set; }
        public CancellationTokenSource TokenSource { get; private set; }
#nullable enable
        public MailBox? MailBox { get; set; }
#nullable disable
        private Connection() { }

        internal static Connection Create(Socket socket, CancellationTokenSource tokenSource)
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
