using LPS.Core.Debug;
using LPS.Core.Rpc;

namespace LPS.Core
{
    public class DbManager
    {
        public string Ip { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }
        private readonly TcpServer tcpDbManagerServer_;
        

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
        }

        private void RegisterMessageFromServerHandlers()
        {

        }

        private void UnregisterMessageFromServerHandlers()
        {

        }

        public void Stop()
        {
            this.tcpDbManagerServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start dbmanager at {this.Ip}:{this.Port}");

            // Logger.Info("$Clear global cache...");
            // DbHelper.FastGlobalCache.Clear().Wait();
            // Logger.Info("$Clear global cache complete.");

            tcpDbManagerServer_.Run();
            
            tcpDbManagerServer_.WaitForExit();
            Logger.Debug("DbManager Exit.");
        }

    }
}
