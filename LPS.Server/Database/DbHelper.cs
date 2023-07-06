// -----------------------------------------------------------------------
// <copyright file="DbHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database;

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Server.Database.GlobalCache;
using LPS.Server.Database.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Database helper.
/// </summary>
public static class DbHelper
{
#pragma warning disable SA1600
#pragma warning disable CS8618
    /// <summary>
    /// Global cache json definition.
    /// </summary>
    public class DbInfo
    {
        [JsonProperty("dbtype")]
        public string DbType { get; set; }

        [JsonProperty("dbconfig")]
        public DbConfig DbConfig { get; set; }
    }

    public class DbConfig
    {
        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("defaultdb")]
        public string DefaultDb { get; set; }

        [JsonProperty("username")]
        public string UserName { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }
#pragma warning restore CS8618
#pragma warning restore SA1600

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

    private static DbClient databaseClient = null!;

    /// <summary>
    /// Initialize database.
    /// </summary>
    /// <param name="globalCacheInitParam">The initialization parameter.</param>
    /// <param name="databaseClientIdentifier">The identifier of the database client.</param>
    /// <returns>A task.</returns>
    public static async Task Initialize(DbHelper.DbInfo globalCacheInitParam, string databaseClientIdentifier)
    {
        Logger.Info("Start initialize database...");
        FastGlobalCache = Redis.Instance;
        string connectString =
         $"{globalCacheInitParam.DbConfig.Ip}:{globalCacheInitParam.DbConfig.Port}," +
         $"password={globalCacheInitParam.DbConfig.Password},defaultDatabase={globalCacheInitParam.DbConfig.DefaultDb}";
        await FastGlobalCache.Initialize(connectString);

        databaseClient = new DbClient(databaseClientIdentifier);
        databaseClient.Initialize();

        // todo: slow.initialize()
        Logger.Info("Initialize database success.");
    }

    /// <summary>
    /// Invokes a database API with the specified name and arguments.
    /// </summary>
    /// <param name="apiName">The name of the API to invoke.</param>
    /// <param name="args">The arguments to pass to the API.</param>
    /// <typeparam name="T">The type of the API response.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a <see cref="JObject"/>.</returns>
    public static Task<T?> CallDbApi<T>(string apiName, params object[]? args)
    {
        if (databaseClient is null)
        {
            var e = new InvalidOperationException("Database is not initialized.");
            Logger.Error(e);
            throw e;
        }

        return databaseClient.CallDbApi<T?>(apiName, args);
    }

    /// <summary>
    /// Invokes an inner database API with the specified name and arguments.
    /// </summary>
    /// <param name="innerApiName">The name of the inner API to invoke.</param>
    /// <param name="args">The arguments to pass to the inner API.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the inner API as an <see cref="Any"/>.</returns>
    public static Task<Any> CallDbInnerApi(string innerApiName, params Any[] args)
    {
        if (databaseClient is null)
        {
            var e = new InvalidOperationException("Database is not initialized.");
            Logger.Error(e);
            throw e;
        }

        return databaseClient.CallDbInnerApi(innerApiName, args);
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