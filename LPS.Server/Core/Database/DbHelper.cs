// -----------------------------------------------------------------------
// <copyright file="DbHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core.Database
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LPS.Common.Core.Debug;
    using LPS.Server.Core.Database.GlobalCache;

    /// <summary>
    /// Database helper.
    /// </summary>
    public static class DbHelper
    {
        /// <summary>
        /// Gets the fast global cache client.
        /// Fast* client allows current process directly access to cache (redis).
        /// Be careful to use this api.
        /// This api should only be used under latency-sensitive circumstance.
        /// </summary>
        public static IGlobalCache FastGlobalCache { get; private set; } = null!;

        /// <summary>
        /// Gets the slow global cache client.
        /// Slow* client posts the access operation message to remote DbManager process.
        /// DbManager process holds a message queue to control the cache-access frequency.
        /// User should use this api in most cases.
        /// </summary>
        public static readonly IGlobalCache SlowGlobalCache = new MqGlobalCacheAccessor();

        /// <summary>
        /// Gets the fast database client.
        /// </summary>
        public static IDatabase FastDatabase => throw new NotImplementedException();

        /// <summary>
        /// Gets the slow database client.
        /// </summary>
        public static IDatabase SlowDatabase => throw new NotImplementedException();

        /// <summary>
        /// Initialize database.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task Initialize()
        {
            Logger.Info("Start initialize database...");
            FastGlobalCache = Redis.Instance;
            await FastGlobalCache.Initialize();

            // todo: slow.initialize()
            Logger.Info("Initialize database success.");
        }

        /// <summary>
        /// Generate a runtime global-unique id via GlobalCache.
        /// </summary>
        /// <returns>Global-unique id string.</returns>
        public static async Task<string> GenerateNewGlobalId()
        {
            var key = "$_lps_sys_entity_id_counter";
            var numId = await FastGlobalCache.Incr(key);
            return BuildGlobalId(numId);
        }

        private static string BuildGlobalId(long longId)
        {
            // LPS simply use global cache to generate temp entity global id
            // the global id is generated by rule as follow:
            // 1. incr global counter in global cache
            // 2. get global counter's val
            // 3. val % 10e16 (get the last 16 digits)
            // 4. convert val to string and padding to 16-digits string
            // 5. convert 16-digits string to base64 string as the global id
            var globalId = longId;
            var stringId = Convert.ToString(globalId % 10e16, CultureInfo.InvariantCulture).PadLeft(16, '0');
            var newId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stringId));
            return newId;
        }
    }
}