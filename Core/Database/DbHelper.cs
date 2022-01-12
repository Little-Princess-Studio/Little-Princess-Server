using System;
using System.Threading.Tasks;
using LPS.Core.Database.GlobalCache;
using LPS.Core.Debug;

namespace LPS.Core.Database
{

    public static class DbHelper
    {
        private static IGlobalCache? fastGlobalCache_;
        // Fast* client allows current process directly access to cache (redis).
        // Be careful to use this api.
        // This api should only be used under latency-sensitive circumstance.
        public static IGlobalCache FastGlobalCache => fastGlobalCache_!;
        // Slow* client posts the access operation message to remote DbManager process.
        // DbManager process holds a message queue to control the cache-access frequency.
        // User should use this api in most cases.
        public static readonly IGlobalCache SlowGlobalCache = new MqGlobalCacheAccessor();

        public static IDatabase FastDatabase => throw new NotImplementedException();
        public static IDatabase SlowDatabase => throw new NotImplementedException();

        public async static Task Initialize()
        {
            Logger.Info("Start initialize database...");
            fastGlobalCache_ = Redis.Instance;
            await fastGlobalCache_.Initialize();
            // todo: slow.initialzie()
            Logger.Info("Initialize database success.");
        }
    }
}