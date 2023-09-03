// -----------------------------------------------------------------------
// <copyright file="CommandParser.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Console;

using System.Reflection;
using LPS.Client.Console;
using LPS.Common.Debug;

/// <summary>
/// Command parser.
/// </summary>
public static class CommandParser
{
    private static readonly Dictionary<string, (MethodInfo, string[])> CmdMapping = new ();

    /// <summary>
    /// Scan commands.
    /// </summary>
    /// <param name="namespace">Namespace where the scanning will be applying.</param>
    /// <exception cref="Exception">Throw exception if failed to scan.</exception>
    public static void ScanCommands(string @namespace)
    {
        var cmdMethods = Assembly.GetEntryAssembly() !.GetTypes()
            .Select(type => type.GetMethods()
                .Where(method => method.IsStatic && Attribute.IsDefined(method, typeof(ConsoleCommandAttribute))))
            .SelectMany(method => method);

        foreach (var method in cmdMethods)
        {
            if (!ValidateCmdMethod(method))
            {
                throw new Exception($"Invalid cmd method: " +
                                    $"{method.Name}" +
                                    $"({string.Join(',', method.GetParameters().Select(param => param.ParameterType.Name))})");
            }

            var cmdAttr = method.GetCustomAttribute<ConsoleCommandAttribute>() !;
            var args = method.GetParameters()
                .Select(param => $"({param.ParameterType.Name}){param.Name}")
                .ToArray();

            CmdMapping[cmdAttr.Name] = (method, args);

            Logger.Debug($"Register: {cmdAttr.Name} => {method.Name}({string.Join(',', args)})");
        }
    }

    /// <summary>
    /// Find suggestions of input.
    /// </summary>
    /// <param name="prefix">Input prefix.</param>
    /// <returns>Suggestion info.</returns>
    public static (string[] CmdNames, string[] CmdDetails) FindSuggestions(string prefix)
    {
        var cmdNames = CmdMapping
            .Where(kv => kv.Key.StartsWith(prefix))
            .Select(kv => kv.Key)
            .ToArray();
        var details = cmdNames
            .Select(cmdName => $"{cmdName}[{string.Join(',', CmdMapping[cmdName].Item2)}]")
            .ToArray();

        return (cmdNames, details);
    }

    /// <summary>
    /// Returns an array of all command names.
    /// </summary>
    /// <returns>An array of strings representing all command names.</returns>
    public static string[] GetAllCmdNames() => CmdMapping.Keys.ToArray();

    /// <summary>
    /// Dispatch command.
    /// </summary>
    /// <param name="commandString">Command raw string.</param>
    /// <exception cref="Exception">Throw exception if failed to dispatch command.</exception>
    public static void Dispatch(string commandString)
    {
        var cmdArr = commandString.Split(
            ' ',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var cmdName = cmdArr[0];

        if (!CmdMapping.ContainsKey(cmdName))
        {
            throw new Exception($"Invalid cmd name {cmdName}");
        }

        var methodInfo = CmdMapping[cmdName].Item1;
        var argTypes = methodInfo.GetParameters().Select(param => param.ParameterType).ToArray();

        var argCnt = argTypes.Length;

        if (argCnt != cmdArr.Length - 1)
        {
            throw new Exception($"Invalid arguments nums {cmdArr.Length - 1} for {argCnt}");
        }

        if (argCnt > 0)
        {
            var cmdArgs = cmdArr[1..];
            var args = cmdArgs
                .Select((literal, idx) => ConvertCommandStringArgToObject(argTypes[idx], literal))
                .ToArray();
            var res = methodInfo.Invoke(null, args);
        }
        else
        {
            methodInfo.Invoke(null, null);
        }
    }

    private static bool ValidateCmdMethod(MethodInfo methodInfo)
    {
        var returnType = methodInfo.ReturnType;
        if (returnType != typeof(void))
        {
            return false;
        }

        return methodInfo.GetParameters().All(param => CheckArgTypeValid(param.ParameterType));
    }

    private static bool CheckArgTypeValid(Type argType)
    {
        return argType == typeof(string)
               || argType == typeof(int)
               || argType == typeof(float)
               || argType == typeof(bool);
    }

    private static object ConvertCommandStringArgToObject(Type argType, string argLiteral)
    {
        return argType switch
        {
            _ when argType == typeof(string) => argLiteral,
            _ when argType == typeof(bool) => Convert.ToBoolean(argLiteral),
            _ when argType == typeof(int) => Convert.ToInt32(argLiteral),
            _ when argType == typeof(float) => Convert.ToSingle(argLiteral),
            _ => throw new Exception("Invalid "),
        };
    }
}