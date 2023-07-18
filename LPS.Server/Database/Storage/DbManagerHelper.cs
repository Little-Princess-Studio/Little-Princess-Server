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
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Util;
using LPS.Server.Database.Storage;
using LPS.Server.Database.Storage.Attribute;
using Newtonsoft.Json.Linq;

/// <summary>
/// Provides helper methods for managing the database.
/// </summary>
public static class DbManagerHelper
{
    private static readonly Dictionary<string, MethodInfo> DatabaseApiStore = new();
    private static readonly Dictionary<string, MethodInfo> DatabaseInnerApiStore = new();
    private static IDatabase currentDatabase = default!;
    private static string connectString = default!;
    private static object databaseApiInstance = default!;
    private static object databaseInnerApiInstance = default!;

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
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    public static void ScanDbApis(string @namespace, Assembly[]? extraAssemblies = null)
    {
        // scan all the types inside the namespace and has attribute of DbApiProvider
        var types = AttributeHelper.ScanTypeWithNamespace(
            @namespace,
            type => type.IsClass
                && type.GetCustomAttribute<DbApiProviderAttribute>()?.DbType == currentDatabase.GetType(),
            extraAssemblies);

        if (types == null)
        {
            Logger.Warn("No database api provider found.");
            return;
        }

        if (types.Count() > 1)
        {
            Logger.Warn($"Multiple database api provider found, only use the first found one: {types.First().Name}");
        }

        var provider = types.First()!;

        var methods = provider.GetMethods()
            .Where(method => method.GetCustomAttribute<DbApiAttribute>() != null);

        var validated = methods.All(method => RpcHelper.ValidateMethodSignature(method, 1, false))
                    && methods.All(ValidateFirstArg);

        if (!validated)
        {
            var e = new Exception(@namespace + " contains invalid database api provider.");
            Logger.Error(e);
            throw e;
        }

        methods.ToList().ForEach(method =>
        {
            Logger.Info($"Database api provider found: {method.Name}");
            DatabaseApiStore.Add(method.Name, method);
        });

        databaseApiInstance = Activator.CreateInstance(provider)!;

        Logger.Info("Database api provider loaded.");
    }

    /// <summary>
    /// Scans the specified namespace for types that have the <see cref="DbInnerApiProviderAttribute"/> attribute and registers them in the <see cref="DatabaseInnerApiStore"/>.
    /// </summary>
    /// <param name="namespace">The namespace to scan.</param>
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    public static void ScanInnerDbApis(string @namespace, Assembly[]? extraAssemblies = null)
    {
        var types = AttributeHelper.ScanTypeWithNamespace(
            @namespace,
            type => type.IsClass
                && type.GetCustomAttribute<DbInnerApiProviderAttribute>()?.DbType == currentDatabase.GetType(),
            extraAssemblies);

        if (types == null)
        {
            Logger.Warn("No database api provider found.");
            return;
        }

        if (types.Count() > 1)
        {
            Logger.Warn($"Multiple database api provider found, only use the first found one: {types.First().Name}");
        }

        var provider = types.First()!;

        var validated = provider.GetInterfaces().Any(
            @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IDbInnerApi<>)
                && @interface.GetGenericArguments().Length == 1
                && @interface.GetGenericArguments()[0] == currentDatabase.GetType());

        if (!validated)
        {
            var e = new Exception($"Invalid database inner api provider definition: {provider}");
            Logger.Error(e);
            throw e;
        }

        var methods = provider.GetMethods()
            .Where(method => method.GetCustomAttribute<DbInnerApiAttribute>() != null);

        validated = methods.All(ValidateInnerApiSignature) && methods.All(ValidateFirstArg);

        if (!validated)
        {
            var e = new Exception(@namespace + " contains invalid database api provider.");
            Logger.Error(e);
            throw e;
        }

        methods.ToList().ForEach(method =>
        {
            Logger.Info($"Database inner api provider found: {method.Name}");
            DatabaseInnerApiStore.Add(method.Name, method);
        });

        databaseInnerApiInstance = Activator.CreateInstance(provider)!;

        Logger.Info("Database inner api provider loaded.");
    }

    /// <summary>
    /// Calls the specified database API method with the given arguments.
    /// </summary>
    /// <param name="apiName">The name of the database API method to call.</param>
    /// <param name="args">The arguments to pass to the database API method.</param>
    /// <returns>The result of the database API method call.</returns>
    public static Task<object?> CallDbApi(string apiName, Any[] args)
    {
        if (!DatabaseApiStore.TryGetValue(apiName, out var method))
        {
            Logger.Warn($"Database API method not found: {apiName}");
            return Task.FromResult<object?>(null);
        }

        var parameters = method.GetParameters();
        if ((args.Length + 1) != parameters.Length)
        {
            Logger.Warn($"Database API method argument count mismatch: {apiName}, {args.Length} of {parameters.Length}");
            return Task.FromResult<object?>(null);
        }

        List<object> arguments = new() { currentDatabase };
        for (int i = 0; i < args.Length; ++i)
        {
            var parsed = RpcHelper.ProtoBufAnyToRpcArg(args[i], parameters[i].ParameterType)!;
            arguments.Add(parsed);
        }

        try
        {
            var callRes = method.Invoke(databaseApiInstance, arguments.ToArray())!;
            return HandleRpcResult(callRes, method.ReturnType);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            return Task.FromResult<object?>(null);
        }
    }

    /// <summary>
    /// Calls the specified inner database API method with the given arguments.
    /// </summary>
    /// <param name="innerApiName">The name of the inner database API method to call.</param>
    /// <param name="args">The arguments to pass to the inner database API method.</param>
    /// <returns>The result of the inner database API method call.</returns>
    public static Task<Any> CallInnerDbApi(string innerApiName, Any[] args)
    {
        if (!DatabaseInnerApiStore.TryGetValue(innerApiName, out var method))
        {
            Logger.Warn($"Database API method not found: {innerApiName}");
            return Task.FromResult<Any>(Any.Pack(new NullArg()));
        }

        var arguments = new object[2] { currentDatabase, args };

        try
        {
            var callRes = method.Invoke(databaseInnerApiInstance, arguments)!;
            return (callRes as Task<Any>)!;
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            return Task.FromResult<Any>(Any.Pack(new NullArg()));
        }
    }

    private static Task<object?> HandleTask<T>(in Task<T> task) => task.ContinueWith(t => (object?)t.Result);

    private static Task<object?> HandleTask(in Task task) => task.ContinueWith(_ => (object?)null);

    private static Task<object?> HandleValueTask<T>(in ValueTask<T> task)
    {
        if (task.IsCompleted)
        {
            return Task.FromResult((object?)task.Result);
        }
        else
        {
            return task.AsTask().ContinueWith(t => (object?)t.Result);
        }
    }

    private static Task<object?> HandleValueTask(in ValueTask task)
    {
        if (task.IsCompleted)
        {
            return Task.FromResult((object?)null);
        }
        else
        {
            return task.AsTask().ContinueWith(t => (object?)null);
        }
    }

    private static async Task<object?> HandleTaskDynamic(dynamic task)
    {
        var res = await task;
        return res;
    }

    private static Task<object?> HandleRpcResult(object callRes, System.Type returnType)
    {
        var type = returnType;
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() != typeof(Task<>) && type.GetGenericTypeDefinition() != typeof(ValueTask<>))
        {
            var e = new Exception("Database API method return type must be Task or ValueTask.");
            Logger.Error(e);
            throw e;
        }
        else if (!type.IsGenericType && type != typeof(ValueTask) && type != typeof(Task))
        {
            var e = new Exception("Database API method return type must be Task or ValueTask.");
            Logger.Error(e);
            throw e;
        }

        if (type.IsGenericType)
        {
            switch (callRes)
            {
                case Task<int> task:
                    return HandleTask(task);
                case Task<float> task:
                    return HandleTask(task);
                case Task<string> task:
                    return HandleTask(task);
                case Task<bool> task:
                    return HandleTask(task);
                case Task<Common.Rpc.MailBox> task:
                    return HandleTask(task);
                case ValueTask<int> task:
                    return HandleValueTask(task);
                case ValueTask<float> task:
                    return HandleValueTask(task);
                case ValueTask<string> task:
                    return HandleValueTask(task);
                case ValueTask<bool> task:
                    return HandleValueTask(task);
                case ValueTask<Common.Rpc.MailBox> task:
                    return HandleValueTask(task);
                default:
                    {
                        dynamic task = callRes;
                        return HandleTaskDynamic(task);
                    }
            }
        }
        else
        {
            switch (callRes)
            {
                case Task task:
                    return HandleTask(task);
                case ValueTask task:
                    return HandleValueTask(task);
                default:
                    {
                        var e = new Exception("Database API method return type must be Task or ValueTask.");
                        Logger.Error(e);
                        throw e;
                    }
            }
        }
    }

    private static bool ValidateFirstArg(MethodInfo method)
    {
        var args = method.GetParameters().Select(p => p.ParameterType).ToArray();
        if (args.Length == 0)
        {
            var e = new Exception("Database api provider's first parameter type mismatch. No parameter.");
            Logger.Error(e);
            return false;
        }

        var first = args.First();
        if (first != currentDatabase.GetType())
        {
            var e = new Exception($"Database api provider's first parameter type mismatch. {first} for {currentDatabase.GetType()}.");
            Logger.Error(e);
            return false;
        }

        return true;
    }

    private static bool ValidateInnerApiSignature(MethodInfo method)
    {
        var args = method.GetParameters();
        if (args.Length != 2)
        {
            Logger.Warn($"Invalid method parameter list of {method.Name}");
            return false;
        }

        if (args[0].ParameterType != currentDatabase.GetType() || args[1].ParameterType != typeof(Any[]))
        {
            Logger.Warn($"Invalid method parameter list of {method.Name}");
            return false;
        }

        var returnType = method.ReturnType;

        if (returnType != typeof(Task<Any>))
        {
            Logger.Warn($"Return type of {method.Name} must be Task<Any>.");
            return false;
        }

        return true;
    }
}
