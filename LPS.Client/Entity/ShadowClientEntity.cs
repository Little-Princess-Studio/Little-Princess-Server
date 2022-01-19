using LPS.Core.Entity;
using LPS.Core.Rpc;

namespace LPS.Client.Entity
{
    [EntityClass]
    public class ServerProxy
    {
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
    
    public class ShadowClientEntity : ShadowEntity
    {
        public readonly ServerProxy Server = new ServerProxy();
    }
}
