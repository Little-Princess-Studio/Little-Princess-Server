// -----------------------------------------------------------------------
// <copyright file="RpcStubGeneratorManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

using System.Collections.ObjectModel;
using System.Reflection;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Util;

/// <summary>
/// Manages the generation of RPC stubs.
/// </summary>
public static class RpcStubGeneratorManager
{
    private static ReadOnlyDictionary<uint, RpcStubGenerator> generatorMap = null!;

    /// <summary>
    /// Scans the specified namespace for types decorated with the <see cref="RpcStubAttribute"/> attribute and builds a generator for each type found.
    /// </summary>
    /// <param name="rpcStubInterfaceNamespaces">The namespaces to scan rpc stub interfaces.</param>
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    public static void ScanAndBuildGenerator(
        string[] rpcStubInterfaceNamespaces,
        Assembly[]? extraAssemblies = null)
    {
        var dict = new Dictionary<uint, RpcStubGenerator>();

        foreach (var @namespace in rpcStubInterfaceNamespaces)
        {
            Logger.Info($"Scanning {@namespace} for RPC stub generators...");

            var stubInterfaces = AttributeHelper.ScanTypeWithNamespaceAndAttribute(
                @namespace,
                typeof(RpcStubAttribute),
                true,
                type => type.IsInterface,
                extraAssemblies);

            Dictionary<Type, List<Type>> dictGeneratorTypeToStubInterfaces = new();

            foreach (var interfaceType in stubInterfaces)
            {
                var attr = interfaceType.GetCustomAttribute<RpcStubAttribute>()!;
                var generatorType = attr.GeneratorType;
                if (generatorType == typeof(RpcStubGenerator) || generatorType.IsSubclassOf(typeof(RpcStubGenerator)))
                {
                    if (!dictGeneratorTypeToStubInterfaces.TryGetValue(generatorType, out var list))
                    {
                        list = new List<Type>() { interfaceType };
                        dictGeneratorTypeToStubInterfaces.Add(generatorType, list);
                    }
                    else
                    {
                        list.Add(interfaceType);
                    }
                }
                else
                {
                    throw new Exception($"The specified generator type {generatorType.FullName} must be a subclass of RpcStubGenerator");
                }
            }

            foreach (var (generatorType, rpcStubInterfaceTypeList) in dictGeneratorTypeToStubInterfaces)
            {
                Logger.Info(
                    $"Generated stub generator {generatorType.FullName} for" +
                    $" {string.Join(",", rpcStubInterfaceTypeList.Select(t => t.FullName))}");

                var generator = (Activator.CreateInstance(generatorType) as RpcStubGenerator)!;
                generator.GenerateStubTypeWithRpcStubList(rpcStubInterfaceTypeList);
                foreach (var rpcStubInterfaceType in rpcStubInterfaceTypeList)
                {
                    dict.Add(TypeIdHelper.GetId(rpcStubInterfaceType), generator);
                }
            }
        }

        generatorMap = new(dict);
    }

    /// <summary>
    /// Gets the RPC stub generator for the specified interface type.
    /// </summary>
    /// <param name="interfaceType">The type of the stub interface for which to get the RPC stub generator.</param>
    /// <returns>The RPC stub generator for the specified interface class type.</returns>
    /// <exception cref="Exception">Thrown when the RPC stub generator for the specified interface type cannot be found.</exception>
    public static RpcStubGenerator GetRpcStubGenerator(Type interfaceType)
    {
        var id = TypeIdHelper.GetId(interfaceType);
        if (generatorMap.TryGetValue(id, out var generator))
        {
            return generator;
        }

        throw new Exception($"Failed to find stub generator for {interfaceType.FullName}");
    }

    /// <summary>
    /// Gets the RPC stub generator of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the RPC stub generator to get.</typeparam>
    /// <returns>The RPC stub generator of the specified type.</returns>
    /// <exception cref="Exception">Thrown when the RPC stub generator for the specified type cannot be found.</exception>
    public static RpcStubGenerator GetRpcStubGenerator<T>()
        where T : IRpcStub
    {
        var id = TypeIdHelper.GetId<T>();
        if (generatorMap.TryGetValue(id, out var generator))
        {
            return generator;
        }

        throw new Exception($"Failed to find stub generator for {typeof(T).FullName}");
    }
}