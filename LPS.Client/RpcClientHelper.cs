// -----------------------------------------------------------------------
// <copyright file="RpcClientHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client;

using LPS.Client.Entity;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Ipc;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// Client RPC helper class.
/// </summary>
public static class RpcClientHelper
{
    /// <summary>
    /// A set of allowed generic types for RPC properties.
    /// </summary>
    public static readonly HashSet<Type> AllowedRpcPropertyGenTyeps = new() { typeof(RpcShadowComplexProperty<>), typeof(RpcShadowPlaintProperty<>) };

    private static readonly AsyncTaskGenerator ComponentSyncTaskGenerator = new();
    private static readonly Dictionary<(string EntityId, string ComponentName), uint> ComponentSyncNameToTaskIdMap = new();

    private static readonly Dictionary<string, Type> EntityClassMap = RpcHelper.EntityClassMap;

    /// <summary>
    /// Create client entity.
    /// </summary>
    /// <param name="entityClassName">Entity class name.</param>
    /// <returns><see cref="ShadowClientEntity"/>.</returns>
    /// <exception cref="Exception">Throw exception if failed to create client entity.</exception>
    public static async Task<ShadowClientEntity> CreateClientEntity(string entityClassName)
    {
        if (EntityClassMap.ContainsKey(entityClassName))
        {
            var entityClass = EntityClassMap[entityClassName];
            if (entityClass.IsSubclassOf(typeof(ShadowClientEntity)))
            {
                var obj = (Activator.CreateInstance(entityClass) as ShadowClientEntity)!;
                await obj.InitComponents();
                RpcHelper.BuildPropertyTree(
                    obj,
                    AllowedRpcPropertyGenTyeps,
                    typeof(RpcShadowPlaintProperty<>),
                    typeof(RpcShadowComplexProperty<>),
                    false);
                return obj;
            }

            throw new Exception(
                $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
        }

        throw new Exception($"Invalid entity class name {entityClassName}");
    }

    /// <summary>
    /// Require property full sync to this client's shadow entity.
    /// </summary>
    /// <param name="entityId">shadow entity ID.</param>
    public static void RequirePropertyFullSync(string entityId)
    {
        var requireFullSync = new RequirePropertyFullSync()
        {
            EntityId = entityId,
        };

        Client.Instance.Send(requireFullSync);
        Logger.Info($"require full property sync");
    }

    /// <summary>
    /// Require component sync to this client's shadow entity.
    /// </summary>
    /// <param name="entityId">Shadow entity ID.</param>
    /// <param name="componentName">Name of the component to sync.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task RequireComponentSync(string entityId, string componentName)
    {
        var query = (entityId, componentName);
        if (ComponentSyncNameToTaskIdMap.ContainsKey(query))
        {
            var taskId = ComponentSyncNameToTaskIdMap[query];
            var existingTask = ComponentSyncTaskGenerator.GetTaskById(taskId);
            if (existingTask is null)
            {
                Logger.Warn("Component sync task is null, maybe it has been completed.");
                return Task.CompletedTask;
            }

            return existingTask;
        }

        var (task, asyncId) = ComponentSyncTaskGenerator.GenerateAsyncTask(5000, (_) =>
        {
            ComponentSyncNameToTaskIdMap.Remove(query);
            return new RpcTimeOutException("Component sync time out.");
        });

        ComponentSyncNameToTaskIdMap[query] = asyncId;

        var requireComponentSync = new RequireComponentSync()
        {
            EntityId = entityId,
            ComponentName = componentName,
        };

        Client.Instance.Send(requireComponentSync);

        Logger.Info($"require component {componentName} sync");
        return task;
    }

    /// <summary>
    /// Resolves the component sync task for the given entity ID and component name.
    /// </summary>
    /// <param name="entityId">The ID of the entity.</param>
    /// <param name="componentName">The name of the component.</param>
    public static void ResolveComponetnSyncTask(string entityId, string componentName)
    {
        var query = (entityId, componentName);
        if (ComponentSyncNameToTaskIdMap.ContainsKey(query))
        {
            var taskId = ComponentSyncNameToTaskIdMap[query];

            ComponentSyncNameToTaskIdMap.Remove(query);
            ComponentSyncTaskGenerator.ResolveAsyncTask(taskId);
        }
    }
}