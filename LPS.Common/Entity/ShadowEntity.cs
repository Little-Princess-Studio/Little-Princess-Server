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
    /// Build shadow entity from protobuf.
    /// </summary>
    /// <param name="syncBody">Protobuf data.</param>
    public void FromSyncContent(Any syncBody)
    {
        if (syncBody.Is(DictWithStringKeyArg.Descriptor))
        {
            var content = syncBody.Unpack<DictWithStringKeyArg>();

            foreach (var (key, value) in content.PayLoad)
            {
                if (this.PropertyTree!.ContainsKey(key))
                {
                    RpcProperty? prop = this.PropertyTree[key];
                    prop.FromProtobuf(value);
                }
                else
                {
                    Debug.Logger.Warn($"Missing sync property {key} in {this.GetType()}");
                }
            }
        }
    }

    /// <summary>
    /// Apply sync command list to this entity.
    /// </summary>
    /// <param name="syncCmdList">Sync command list.</param>
    /// <exception cref="Exception">Throw exception if failed to apply.</exception>
    /// <exception cref="ArgumentOutOfRangeException">ArgumentOutOfRangeException.</exception>
    public void ApplySyncCommandList(PropertySyncCommandList syncCmdList)
    {
        var path = syncCmdList.Path.Split('.');
        var propType = syncCmdList.PropType;
        var entityId = syncCmdList.EntityId!;
        if (entityId != this.MailBox.Id)
        {
            throw new Exception($"Not the same entity id {entityId} of {this.MailBox.Id}");
        }

        var container = this.FindContainerByPath(path);
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
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    // let it throw null reference exception if failed casting.
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

    private RpcPropertyContainer FindContainerByPath(string[] path)
    {
        var rootName = path[0];

        if (!this.PropertyTree!.ContainsKey(rootName))
        {
            throw new Exception($"Invalid root path name {rootName}");
        }

        var container = this.PropertyTree[rootName].Value;

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
}