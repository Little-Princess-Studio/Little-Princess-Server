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
    /// Scans the specified namespace for types decorated with the <see cref="RpcStubGeneratorAttribute"/> attribute and builds a generator for each type found.
    /// </summary>
    /// <param name="namespace">The namespace to scan for types.</param>
    public static void ScanAndBuildGenerator(string @namespace)
    {
        var dict = new Dictionary<uint, RpcStubGenerator>();

        var stubGeneratorAttrs = AttributeHelper.ScanTypeWithNamespaceAndAttribute(
            @namespace,
            typeof(RpcStubGeneratorAttribute),
            true,
            type => type.IsClass);

        foreach (var entityClassType in stubGeneratorAttrs)
        {
            var attr = entityClassType.GetCustomAttribute<RpcStubGeneratorAttribute>()!;
            var generatorType = attr.GeneratorType;
            if (generatorType == typeof(RpcStubGenerator) || generatorType.IsSubclassOf(typeof(RpcStubGenerator)))
            {
                var generator = Activator.CreateInstance(generatorType) as RpcStubGenerator;
                if (generator is not null)
                {
                    dict.Add(TypeIdHelper.GetId(entityClassType), generator);
                    Logger.Info($"Generated stub generator {generatorType.FullName} for {entityClassType.FullName}");
                }
                else
                {
                    throw new Exception($"Failed to create stub generator with {generatorType.FullName} for {entityClassType.FullName}");
                }
            }
            else
            {
                throw new Exception($"The specified generator type {generatorType.FullName} must be a subclass of RpcStubGenerator");
            }
        }

        generatorMap = new(dict);
    }

    /// <summary>
    /// Gets the RPC stub generator for the specified entity class type.
    /// </summary>
    /// <param name="entityClassType">The type of the entity class for which to get the RPC stub generator.</param>
    /// <returns>The RPC stub generator for the specified entity class type.</returns>
    /// <exception cref="Exception">Thrown when the RPC stub generator for the specified entity class type cannot be found.</exception>
    public static RpcStubGenerator GetRpcStubGenerator(Type entityClassType)
    {
        var id = TypeIdHelper.GetId(entityClassType);
        if (generatorMap.TryGetValue(id, out var generator))
        {
            return generator;
        }
        else
        {
            throw new Exception($"Failed to find stub generator for {entityClassType.FullName}");
        }
    }

    /// <summary>
    /// Gets the RPC stub generator of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the RPC stub generator to get.</typeparam>
    /// <returns>The RPC stub generator of the specified type.</returns>
    /// <exception cref="Exception">Thrown when the RPC stub generator for the specified type cannot be found.</exception>
    public static RpcStubGenerator GetRpcStubGenerator<T>()
        where T : BaseEntity
    {
        var id = TypeIdHelper.GetId<T>();
        if (generatorMap.TryGetValue(id, out var generator))
        {
            return generator;
        }
        else
        {
            throw new Exception($"Failed to find stub generator for {typeof(T).FullName}");
        }
    }
}
