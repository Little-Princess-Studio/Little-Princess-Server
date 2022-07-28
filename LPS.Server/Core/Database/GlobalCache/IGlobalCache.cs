using System.Threading.Tasks;

namespace LPS.Server.Core.Database.GlobalCache
{
    public interface IGlobalCache<T>
    {
        Task<bool> Set(string key, T val);
        Task<T> Get(string key);
    }

    public interface IGlobalCache : IGlobalCache<long>, IGlobalCache<string>
    {
        Task<bool> Initialize();
        Task Clear();
        Task<long> Incr(string key);
        object GetNativeClient();
    }
}
