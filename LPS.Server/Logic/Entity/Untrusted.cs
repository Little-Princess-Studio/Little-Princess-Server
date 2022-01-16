using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;

namespace LPS.Logic.Entity
{
    public class ClientProxy
    {
        // gateConnection_ record which gate the client is connecting to
        private Connection gateConnection_;

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
    public class Untrusted : DistributeEntity
    {
        public ClientProxy Client { get; private set; } = null!;
        
        public Untrusted(string desc) : base(desc)
        {
            Logger.Debug($"Untrusted created, desc : {desc}");
        }

        public void BindGateConn(Connection gateConnection)
        {
            this.Client = new ClientProxy(gateConnection);
        }
    }
}
