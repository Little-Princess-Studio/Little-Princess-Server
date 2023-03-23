// -----------------------------------------------------------------------
// <copyright file="RpcClientHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client;

using System.Reflection;
using LPS.Client.Entity;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Entity;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Client RPC helper class.
/// </summary>
public static class RpcClientHelper
{
    private static readonly Dictionary<string, Type> EntityClassMap = RpcHelper.EntityClassMap;

    /// <summary>
    /// Create client entity.
    /// </summary>
    /// <param name="entityClassName">Entity class name.</param>
    /// <returns><see cref="ShadowClientEntity"/>.</returns>
    /// <exception cref="Exception">Throw exception if failed to create client entity.</exception>
    public static ShadowClientEntity CreateClientEntity(string entityClassName)
    {
        if (EntityClassMap.ContainsKey(entityClassName))
        {
            var entityClass = EntityClassMap[entityClassName];
            if (entityClass.IsSubclassOf(typeof(ShadowClientEntity)))
            {
                var obj = (Activator.CreateInstance(entityClass) as ShadowClientEntity)!;
                BuildPropertyTree(obj);
                return obj;
            }

            throw new Exception(
                $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
        }

        throw new Exception($"Invalid entity class name {entityClassName}");
    }

    /// <summary>
    /// Build property tree for entity.
    /// </summary>
    /// <param name="entity">Entity need to create entity with.</param>
    public static void BuildPropertyTree(BaseEntity entity)
    {
        var type = entity.GetType();
        var tree = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field =>
            {
                var fieldType = field.FieldType;

                if (!fieldType.IsGenericType)
                {
                    return false;
                }

                var genType = fieldType.GetGenericTypeDefinition();
                if (genType != typeof(RpcShadowComplexProperty<>)
                    && genType != typeof(RpcShadowPlaintProperty<>))
                {
                    return false;
                }

                return true;
            }).ToDictionary(
                field => (field.GetValue(entity) as RpcProperty)!.Name,
                field => (field.GetValue(entity) as RpcProperty)!);

        foreach (var (_, prop) in tree)
        {
            prop.Owner = entity;
        }

        entity.SetPropertyTree(tree);
    }
}