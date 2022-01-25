using System.Threading.Tasks;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    public class ClientProxy
    {
        // gateConnection_ record which gate the client is connecting to
        private Connection gateConnection_;
        public Connection GateConn => gateConnection_;

        public ClientProxy(Connection gateConnection)
        {
            gateConnection_ = gateConnection;
        }

        public Task<T> Call<T>(string methodName, params object?[] args)
        {
            return null!;
        }

        public Task Call(string methodName, params object?[] args)
        {
            return null!;
        }

        public void Notify(string methodName, params object?[] args)
        {
            
        }
    }

    [EntityClass]
    public class ServerClientEntity : DistributeEntity
    {
        public ClientProxy Client { get; private set; } = null!;

        protected ServerClientEntity(string desc): base(desc)
        {
        }

        public void BindGateConn(Connection gateConnection)
        {
            this.Client = new ClientProxy(gateConnection);
        }
    }
}
