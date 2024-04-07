// -----------------------------------------------------------------------
// <copyright file="RpcServerHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Common.Rpc;
using LPS.Server.Entity;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Rpc serve helper class.
/// </summary>
public static class RpcServerHelper
{
    /// <summary>
    /// A set of allowed generic types for RPC properties.
    /// </summary>
    public static readonly HashSet<Type> AllowedRpcPropertyGenTypes = new() { typeof(RpcPlaintProperty<>), typeof(RpcComplexProperty<>) };

    private static Dictionary<string, Type> EntityClassMap => RpcHelper.EntityClassMap;

    /// <summary>
    /// Create a local entity.
    /// </summary>
    /// <param name="entityClassName">Entity class name.</param>
    /// <param name="desc">Description string to construct the entity.</param>
    /// <returns>DistributeEntity object.</returns>
    /// <exception cref="Exception">Throw exception if failed to create entity.</exception>
    public static async Task<DistributeEntity> CreateEntityLocally(string entityClassName, string desc)
    {
        if (EntityClassMap.ContainsKey(entityClassName))
        {
            var entityClass = EntityClassMap[entityClassName];
            if (entityClass.IsSubclassOf(typeof(DistributeEntity)))
            {
                var obj = (Activator.CreateInstance(entityClass, desc) as DistributeEntity)!;
                await obj.InitComponents();
                RpcHelper.BuildPropertyTree(
                    obj,
                    AllowedRpcPropertyGenTypes,
                    typeof(RpcPlaintProperty<>),
                    typeof(RpcComplexProperty<>),
                    true);
                return obj;
            }

            throw new Exception(
                $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
        }

        throw new Exception($"Invalid entity class name {entityClassName}");
    }

    /// <summary>
    /// Create an entity in any Server process and get the mailbox of the entity.
    /// </summary>
    /// <param name="entityClassName">Entity class name.</param>
    /// <param name="desc">Description string to construct the entity.</param>
    /// <returns>Async task whose value is the mailbox of the created entity.</returns>
    public static async Task<MailBox> CreateDistributeEntityAnywhere(string entityClassName, string desc)
    {
        var server = ServerGlobal.Server;
        return await server.CreateEntityAnywhere(entityClassName, desc, string.Empty);
    }

    /// <summary>
    /// Create a new ServerClientEntity in any Server process and get the mailbox of the entity.
    /// This method will require to create a new ServerClientEntity with the same gate connection
    /// as the original ServerClientEntity.
    /// </summary>
    /// <param name="entityClassName">Entity class name.</param>
    /// <param name="desc">Entity description.</param>
    /// <param name="entity">ServerClientEntity used to create new ServerClientEntity.</param>
    /// <returns>Async task whose value is the mailbox of the created entity.</returns>
    public static async Task<MailBox> CreateServerClientEntityAnywhere(
        string entityClassName,
        string desc,
        ServerClientEntity entity)
    {
        var server = ServerGlobal.Server;
        var gateId = entity.Client.GateConn.MailBox.Id;
        return await server.CreateEntityAnywhere(entityClassName, desc, gateId);
    }

    /// <summary>
    /// Build an entity from the serialized data.
    /// </summary>
    /// <param name="entityMailBox">Mailbox of the entity.</param>
    /// <param name="entityClassName">Entity class name.</param>
    /// <returns>Built DistributeEntity object.</returns>
    /// <exception cref="Exception">Throw exception if failed to build entity.</exception>
    public static DistributeEntity BuildEntityFromSerialContent(
        MailBox entityMailBox, string entityClassName)
    {
        // var entity = Activator.CreateInstance<DistributeEntity>(entityClassName);
        if (EntityClassMap.ContainsKey(entityClassName))
        {
            var entityClass = EntityClassMap[entityClassName];
            if (entityClass.IsSubclassOf(typeof(DistributeEntity)))
            {
                var obj = (Activator.CreateInstance(entityClass, null) as DistributeEntity)!;
                obj.MailBox = entityMailBox;
                RpcHelper.BuildPropertyTree(
                    obj,
                    AllowedRpcPropertyGenTypes,
                    typeof(RpcPlaintProperty<>),
                    typeof(RpcComplexProperty<>),
                    true);
                return obj;
            }

            throw new Exception(
                $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
        }

        throw new Exception($"Invalid entity class name {entityClassName}");
    }
}