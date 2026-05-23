// -----------------------------------------------------------------------
// <copyright file="ShadowEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Shadow entity is the readonly entity related to another entity, and automatically do properties sync with that entity.
/// <para>
/// As of the server-side shadow rollout the actual property-tree apply
/// machinery lives on <see cref="BaseEntity"/> so non-ShadowEntity classes
/// (e.g. server-side <c>DistributeEntity</c> subclasses instantiated in
/// shadow mode via <c>RpcServerHelper.CreateShadowEntityLocally</c>) can
/// also receive sync commands. ShadowEntity remains as the client-side base
/// for entities that are PURELY shadows (no ori on this process), keeping
/// the historical client UX.
/// </para>
/// </summary>
[EntityClass]
public abstract class ShadowEntity : BaseEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShadowEntity"/> class.
    /// </summary>
    public ShadowEntity()
    {
        this.IsFrozen = true;

        // Intentionally NOT setting IsShadow=true here. ShadowClientEntity
        // (and its property containers like RpcShadowPlaintProperty) already
        // carry the Shadow flag at the RpcPropertySetting level so writes are
        // blocked the existing way. Setting IsShadow on the entity also
        // blocks ServerProxy.Notify (BaseEntity.Notify checks IsFrozen but
        // the IsShadow check would interfere with the client's pre-login
        // RPC handshake during the IsFrozen window).
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
    /// Build shadow entity from protobuf. Delegates to <see cref="BaseEntity.FromSyncContent"/>.
    /// </summary>
    /// <param name="syncBody">Protobuf data.</param>
    public new void FromSyncContent(Any syncBody) => base.FromSyncContent(syncBody);

    /// <summary>
    /// Apply sync command list to this entity. Delegates to <see cref="BaseEntity.ApplySyncCommandList"/>.
    /// </summary>
    /// <param name="syncCmdList">Sync command list.</param>
    /// <param name="isComponentProperty">True if the property is a component property.</param>
    /// <param name="componentName">The name of the component.</param>
    public new void ApplySyncCommandList(PropertySyncCommandList syncCmdList, bool isComponentProperty, string componentName)
        => base.ApplySyncCommandList(syncCmdList, isComponentProperty, componentName);

    /// <summary>
    /// Synchronizes the component with the specified name by deserializing the provided content.
    /// Delegates to <see cref="BaseEntity.SyncComponent"/>.
    /// </summary>
    /// <param name="componentName">The name of the component to synchronize.</param>
    /// <param name="propertyTree">The serialized content of the component.</param>
    public new void SyncComponent(string componentName, Any propertyTree)
        => base.SyncComponent(componentName, propertyTree);
}
