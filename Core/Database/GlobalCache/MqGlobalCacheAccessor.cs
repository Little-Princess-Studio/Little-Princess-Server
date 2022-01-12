using System.Threading.Tasks;

namespace LPS.Core.Database.GlobalCache
{
    public class MqGlobalCacheAccessor : IGlobalCache
    {
        public Task Clear()
        {
            throw new System.NotImplementedException();
        }

        public Task<int> Get(string key)
        {
            throw new System.NotImplementedException();
        }

        public object GetNativeClient()
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Initialize()
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Set(string key, int val)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Set(string key, string val)
        {
            throw new System.NotImplementedException();
        }

        Task<string> IGlobalCache<string>.Get(string key)
        {
            throw new System.NotImplementedException();
        }
    }
}