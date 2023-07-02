// -----------------------------------------------------------------------
// <copyright file="DbManagerHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LPS.Common.Debug;
using LPS.Server.Database.Storage;
using LPS.Server.Database.Storage.Attribute;
using Newtonsoft.Json.Linq;

/// <summary>
/// Provides helper methods for managing the database.
/// </summary>
public static class DbManagerHelper
{
    private static readonly Dictionary<string, MethodInfo> DatabaseApiStore = new();
    private static IDatabase currentDatabase = default!;
    private static string connectString = default!;

    /// <summary>
    /// Initializes static members of the <see cref="DbManagerHelper"/> class.
    /// </summary>
    /// <param name="database">Database object.</param>
    /// <param name="connString">connectString used to connect to database.</param>
    public static void SetDatabase(IDatabase database, string connString)
    {
        currentDatabase = database;
        connectString = connString;
    }

    /// <summary>
    /// Initializes the current database.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the initialization was successful.</returns>
    public static Task<bool> Init() => currentDatabase.Initialize(connectString);

    /// <summary>
    /// Scans the specified namespace for types that have the <see cref="DbApiProviderAttribute"/> attribute and registers them in the <see cref="DatabaseApiStore"/>.
    /// </summary>
    /// <param name="namespace">The namespace to scan.</param>
    public static void ScanDbApis(string @namespace)
    {
        // scan all the types inside the namespace and has attribute of DbApiProvider
        var typesEntry = Assembly.GetEntryAssembly()!.GetTypes()
            .Where(
                type => type.IsClass
                        && type.Namespace == @namespace
                        && type.GetCustomAttribute<DbApiProviderAttribute>()?.DbType == currentDatabase.GetType());

        var types = Assembly.GetCallingAssembly().GetTypes()
            .Where(
                type => type.IsClass
                        && type.Namespace == @namespace
                        && type.GetCustomAttribute<DbApiProviderAttribute>()?.DbType == currentDatabase.GetType())
            .Concat(typesEntry)
            .Distinct()
            .ToList();

        if (types == null)
        {
            Logger.Warn("No database api provider found.");
            return;
        }

        if (types.Count > 1)
        {
            Logger.Warn($"Multiple database api provider found, only use the first found one: {types.First().Name}");
        }

        var provider = types.First()!;

        provider.GetMethods()
            .Where(method => method.GetCustomAttribute<DbApiAttribute>() != null)
            .ToList()
            .ForEach(method =>
            {
                Logger.Info($"Database api provider found: {method.Name}");
                DatabaseApiStore.Add(method.Name, method);
            });

        Logger.Info("Database api provider loaded.");
    }

    /// <summary>
    /// Calls the specified database API method with the given arguments.
    /// </summary>
    /// <param name="apiName">The name of the database API method to call.</param>
    /// <param name="args">The arguments to pass to the database API method.</param>
    /// <returns>The result of the database API method call.</returns>
    public static Task<IDbDataSet?> CallDbApi(string apiName, JArray args)
    {
        if (!DatabaseApiStore.TryGetValue(apiName, out var method))
        {
            Logger.Warn($"Database API method not found: {apiName}");
            return Task.FromResult<IDbDataSet?>(null);
        }

        var parameters = method.GetParameters();
        if (args.Count != parameters.Length)
        {
            Logger.Warn($"Database API method argument count mismatch: {apiName}, {args.Count} of {parameters.Length}");
            return Task.FromResult<IDbDataSet?>(null);
        }

        List<object> arguments = new() { currentDatabase };
        for (int i = 0; i < args.Count; ++i)
        {
            var token = args[i];
            var res = token.ToObject(parameters[i].ParameterType);
            if (res == null)
            {
                Logger.Warn($"Database API method argument type mismatch: {apiName}, parameter {i}, {token.Type} of {parameters[i].ParameterType}");
                return Task.FromResult<IDbDataSet?>(null);
            }

            arguments.Add(res);
        }

        try
        {
            var callRes = method.Invoke(null, arguments.ToArray()) as Task<IDbDataSet?>;
            return callRes ?? Task.FromResult<IDbDataSet?>(null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            return Task.FromResult<IDbDataSet?>(null);
        }
    }
}
