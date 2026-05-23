// -----------------------------------------------------------------------
// <copyright file="BaseEntity.ShadowSync.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity;

using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Shadow-receiver methods on <see cref="BaseEntity"/>. Lifted from the
/// historical client-only <see cref="ShadowEntity"/> so any entity - including
/// a <c>DistributeEntity</c> instantiated in shadow mode on the server side
/// via <c>RpcServerHelper.CreateShadowEntityLocally</c> - can apply inbound
/// PropertySyncCommandList / PropertyFullSync messages via a single API.
/// <para>
/// The setter guard in <see cref="RpcProperty.IsShadowProperty"/> (which now
/// also consults <c>Owner.IsShadow</c>) ensures that writes are still blocked
/// even though every entity now has these read-side apply methods.
/// </para>
/// </summary>
public abstract partial class BaseEntity
{
    /// <summary>
    /// Build entity property tree from a serialized full-sync protobuf body.
    /// </summary>
    /// <param name="syncBody">Protobuf full-sync content.</param>
    public void FromSyncContent(Any syncBody) =>
        this.BuildPropertyTreeByContent(syncBody, out var _);

    /// <summary>
    /// Apply a property-sync command list to this entity.
    /// </summary>
    /// <param name="syncCmdList">Sync command list (PropertySyncCommandList protobuf).</param>
    /// <param name="isComponentProperty">True if the property path lives under a component.</param>
    /// <param name="componentName">Component name (only used when isComponentProperty is true).</param>
    public void ApplySyncCommandList(
        PropertySyncCommandList syncCmdList,
        bool isComponentProperty,
        string componentName)
    {
        var path = syncCmdList.Path.Split('.');

        var entityId = syncCmdList.EntityId!;
        if (entityId != this.MailBox.Id)
        {
            throw new Exception($"Not the same entity id {entityId} of {this.MailBox.Id}");
        }

        var container = isComponentProperty
            ? FindContainerByPathInComponent(this, componentName, path)
            : FindContainerByPath(this, path);

        foreach (var syncCmd in syncCmdList.SyncArg)
        {
            var op = syncCmd.Operation;
            switch (op)
            {
                case SyncOperation.SetValue:
                    HandleSetValue(container, syncCmd.Args);
                    break;
                case SyncOperation.UpdatePair:
                    HandleUpdateDict(container, syncCmd.Args);
                    break;
                case SyncOperation.AddListElem:
                    HandleAddListElem(container, syncCmd.Args);
                    break;
                case SyncOperation.RemoveElem:
                    HandleRemoveElem(container, syncCmd.Args);
                    break;
                case SyncOperation.Clear:
                    HandleClear(container);
                    break;
                case SyncOperation.InsertElem:
                    HandleInsertElem(container, syncCmd.Args);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Invalid sync operation.", nameof(op));
            }
        }
    }

    /// <summary>
    /// Apply a component full-sync.
    /// </summary>
    /// <param name="componentName">Component name.</param>
    /// <param name="propertyTree">Serialized component content.</param>
    public void SyncComponent(string componentName, Any propertyTree)
    {
        if (!this.ComponentNameToComponentTypeId.TryGetValue(componentName, out var componentTypeId))
        {
            throw new Exception($"Component {componentName} not found.");
        }

        var component = this.Components[componentTypeId];
        component.Deserialize(propertyTree);
    }

    private static void HandleInsertElem(RpcPropertyContainer container, RepeatedField<Any> syncArg) =>
        (container as ISyncOpActionInsertElem)!.Apply(syncArg);

    private static void HandleClear(RpcPropertyContainer container) =>
        (container as ISyncOpActionClear)!.Apply();

    private static void HandleRemoveElem(RpcPropertyContainer container, RepeatedField<Any> syncArg) =>
        (container as ISyncOpActionRemoveElem)!.Apply(syncArg);

    private static void HandleAddListElem(RpcPropertyContainer container, RepeatedField<Any> syncArg) =>
        (container as ISyncOpActionAddElem)!.Apply(syncArg);

    private static void HandleUpdateDict(RpcPropertyContainer container, RepeatedField<Any> syncArg) =>
        (container as ISyncOpActionUpdatePair)!.Apply(syncArg);

    private static void HandleSetValue(RpcPropertyContainer container, RepeatedField<Any> syncArg) =>
        (container as ISyncOpActionSetValue)!.Apply(syncArg);

    private static RpcPropertyContainer FindContainerInPropertyTreeByPath(
        string[] path,
        Dictionary<string, RpcProperty>? propertyTree)
    {
        var rootName = path[0];
        if (!propertyTree!.ContainsKey(rootName))
        {
            throw new Exception($"Invalid root path name {rootName}");
        }

        var container = propertyTree[rootName].Value;
        for (int i = 1; i < path.Length; ++i)
        {
            var nodeName = path[i];
            if (container.Children != null && container.Children.ContainsKey(nodeName))
            {
                container = container.Children[nodeName];
            }
            else
            {
                throw new Exception($"Invalid sync path {string.Join('.', path)}, node {nodeName} not found.");
            }
        }

        return container;
    }

    private static RpcPropertyContainer FindContainerByPath(BaseEntity entity, string[] path)
    {
        return FindContainerInPropertyTreeByPath(path, entity.PropertyTree);
    }

    private static RpcPropertyContainer FindContainerByPathInComponent(BaseEntity entity, string componentName, string[] path)
    {
        if (!entity.ComponentNameToComponentTypeId.TryGetValue(componentName, out var componentTypeId))
        {
            throw new Exception($"Component {componentName} not found.");
        }

        var component = entity.Components[componentTypeId];
        return FindContainerInPropertyTreeByPath(path, component.PropertyTree);
    }
}
