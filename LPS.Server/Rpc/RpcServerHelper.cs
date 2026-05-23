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
        return await CreateEntityLocallyInternal(entityClassName, desc, asShadow: false);
    }

    /// <summary>
    /// Create a local entity in shadow mode. The returned entity:
    /// (a) has <see cref="LPS.Common.Entity.BaseEntity.IsShadow"/> set to true
    ///     BEFORE its RpcProperty tree is built, so the property tree picks up
    ///     the shadow flag via <see cref="LPS.Common.Rpc.RpcProperty.RpcProperty.IsShadowProperty"/>
    ///     (which now consults <c>Owner.IsShadow</c>);
    /// (b) silently rejects any RpcProperty mutation (setter throws);
    /// (c) is expected to receive PropertySyncCommandList / PropertyFullSync
    ///     messages from the Gate (forwarded from the ori-server) and apply
    ///     them via the existing sync application path.
    ///
    /// The same class definitions used for ori entities (e.g. <c>Player</c>,
    /// <c>Untrusted</c>) are reused as shadows - no shadow-specific subclass
    /// hierarchy is required.
    /// </summary>
    /// <param name="entityClassName">Entity class name (same registry as ori).</param>
    /// <param name="desc">Description string to construct the entity.</param>
    /// <returns>DistributeEntity in shadow mode.</returns>
    public static async Task<DistributeEntity> CreateShadowEntityLocally(string entityClassName, string desc)
    {
        return await CreateEntityLocallyInternal(entityClassName, desc, asShadow: true);
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

    private static async Task<DistributeEntity> CreateEntityLocallyInternal(
        string entityClassName,
        string desc,
        bool asShadow)
    {
        if (EntityClassMap.ContainsKey(entityClassName))
        {
            var entityClass = EntityClassMap[entityClassName];
            if (entityClass.IsSubclassOf(typeof(DistributeEntity)))
            {
                var obj = (Activator.CreateInstance(entityClass, desc) as DistributeEntity)!;

                // CRITICAL ORDER: flip IsShadow BEFORE InitComponents + BuildPropertyTree.
                // RpcProperty.IsShadowProperty consults Owner.IsShadow lazily, so as long
                // as Owner is set before any setter runs, the guard is effective. But
                // flipping after the tree is built risks an InitComponents-time setter
                // succeeding then suddenly failing post-flip.
                if (asShadow)
                {
                    obj.IsShadow = true;
                }

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
}