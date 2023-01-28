using System.Collections.Concurrent;
using Google.Protobuf;
using LPS.Common.Core.Debug;
using LPS.Server.Core.Rpc;

namespace LPS.Server.Core
{
    public class DbManager : IInstance
    {
        public string Ip { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        public InstanceType InstanceType => InstanceType.DbManager;
        
        private readonly TcpServer tcpDbManagerServer_;
        private readonly TcpClient clientToHostManager_;

        public DbManager(string ip, int port, int hostNum, string hostManagerIp, int hostManagerPort,
            (string IP, int Port, string DefaultDb) cacheInfo)
        {
            this.Ip = ip;
            this.Port = port;
            this.HostNum = hostNum;

            // tcp gate server handles msg from server/other gates
            tcpDbManagerServer_ = new(ip, port)
            {
                OnInit = this.RegisterMessageFromServerHandlers,
                OnDispose = this.UnregisterMessageFromServerHandlers
            };
            
            clientToHostManager_ = new TcpClient(hostManagerIp, hostManagerPort, new ConcurrentQueue<(TcpClient, IMessage, bool)>());
        }

        private void RegisterMessageFromServerHandlers()
        {

        }

        private void UnregisterMessageFromServerHandlers()
        {

        }

        public void Stop()
        {
            clientToHostManager_.Stop();
            tcpDbManagerServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start dbmanager at {this.Ip}:{this.Port}");
            
            tcpDbManagerServer_.Run();
            clientToHostManager_.Run();
            
            clientToHostManager_.WaitForExit();
            tcpDbManagerServer_.WaitForExit();
            Logger.Debug("DbManager Exit.");
        }

    }
}
