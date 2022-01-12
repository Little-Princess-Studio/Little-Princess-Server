using System.Threading.Tasks;

namespace LPS.Core.Database.GlobalCache
{
    public interface IGlobalCache<T>
{
    Task<bool> Set(string key, T val);
    Task<T> Get(string key);
}

public interface IGlobalCache : IGlobalCache<int>, IGlobalCache<string>
{
    Task<bool> Initialize();
    Task Clear();

    object GetNativeClient();
}    
}
