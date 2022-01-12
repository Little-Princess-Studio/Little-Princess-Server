using System;
using System.Threading.Tasks;
using CSRedis;
using LPS.Core.Debug;

namespace LPS.Core.Database.GlobalCache
{
    public class Redis : IGlobalCache
    {
        public static readonly Redis Instance = new ();

        public Task<bool> Initialize()
        {
            string connectString = "127.0.0.1:6379,password=,defaultDatabase=demo";

            Logger.Info($"Connecting to redis with {connectString}");

            try {
                RedisHelper.Initialization(new CSRedisClient("127.0.0.1:6379,password=,defaultDatabase=demo"));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when connecting to redis");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task Clear() => RedisHelper.ScriptFlushAsync();

        // GetNativeClient is a dangerous method which return the native db client object to user
        // user should clearly know what the db client is and do costume opertaion on it.
        public object GetNativeClient() => RedisHelper.Instance;

        public Task<bool> Set(string key, int val)  => RedisHelper.SetAsync(key, val);
        public Task<int> Get(string key) => RedisHelper.GetAsync<int>(key);
        public Task<bool> Set(string key, string val) => RedisHelper.SetAsync(key, val);
        Task<string> IGlobalCache<string>.Get(string key) => RedisHelper.GetAsync<string>(key);
    }
}
