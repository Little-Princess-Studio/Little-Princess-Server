// -----------------------------------------------------------------------
// <copyright file="ShadowEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity;

using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Shadow entity is the readonly entity related to another entity, and automatically do properties sync with that entity.
/// </summary>
[EntityClass]
public class ShadowEntity : BaseEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShadowEntity"/> class.
    /// </summary>
    public ShadowEntity()
    {
        this.IsFrozen = true;
    }

    /// <summary>
    /// This method is called after the properties of the entity have been loaded.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public virtual Task OnLoaded()
    {
        this.IsFrozen = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Build shadow entity from protobuf.
    /// </summary>
    /// <param name="syncBody">Protobuf data.</param>
    public void FromSyncContent(Any syncBody) =>
        this.BuildPropertyTreeByContent(syncBody, out var _);

    /// <summary>
    /// Apply sync command list to this entity.
    /// </summary>
    /// <param name="syncCmdList">Sync command list.</param>
    /// <param name="isComponentProperty">True if the property is a component property.</param>
    /// <param name="componentName">The name of the component.</param>
    /// <exception cref="Exception">Throw exception if failed to apply.</exception>
    /// <exception cref="ArgumentOutOfRangeException">ArgumentOutOfRangeException.</exception>
    public void ApplySyncCommandList(PropertySyncCommandList syncCmdList, bool isComponentProperty, string componentName)
    {
        var path = syncCmdList.Path.Split('.');

        // var propType = syncCmdList.PropType;
        var entityId = syncCmdList.EntityId!;
        if (entityId != this.MailBox.Id)
        {
            throw new Exception($"Not the same entity id {entityId} of {this.MailBox.Id}");
        }

        var container = isComponentProperty ?
            this.FindContainerByPath(path) :
            this.FindContainerByPathInComponent(componentName, path);

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
    /// Synchronizes the component with the specified name by deserializing the provided content.
    /// </summary>
    /// <param name="componentName">The name of the component to synchronize.</param>
    /// <param name="propertyTree">The serialized content of the component.</param>
    public void SyncComponent(string componentName, Any propertyTree)
    {
        if (!this.ComponentNameToComponentTypeId.TryGetValue(componentName, out var componentTypeId))
        {
            throw new Exception($"Component {componentName} not found.");
        }

        var component = this.Components[componentTypeId];

        component.Deserialize(propertyTree);
    }

    // let it throw null reference exception if failed to cast.
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

    private static RpcPropertyContainer FindContainerInPropertyTreeByPath(string[] path, Dictionary<string, RpcProperty>? propertyTree)
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

    private RpcPropertyContainer FindContainerByPath(string[] path)
    {
        var propertyTree = this.PropertyTree;

        return FindContainerInPropertyTreeByPath(path, propertyTree);
    }

    private RpcPropertyContainer FindContainerByPathInComponent(string componentName, string[] path)
    {
        if (!this.ComponentNameToComponentTypeId.TryGetValue(componentName, out var componentTypeId))
        {
            throw new Exception($"Component {componentName} not found.");
        }

        var component = this.Components[componentTypeId];
        var propertyTree = component.PropertyTree;

        return FindContainerInPropertyTreeByPath(path, propertyTree);
    }
}