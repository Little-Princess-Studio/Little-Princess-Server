// -----------------------------------------------------------------------
// <copyright file="ComponentBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

/// <summary>
/// Represents a component that can be attached to an entity.
/// </summary>
public abstract class ComponentBase
{
    /// <summary>
    /// Gets the entity that owns this component.
    /// </summary>
    /// <value>The entity that owns this component.</value>
    public BaseEntity Owner { get; } = null!;

    /// <summary>
    /// Called when the component's entity is initialized.
    /// </summary>
    public abstract void OnInit();

    /// <summary>
    /// Called when the component's entity is destroyed.
    /// </summary>
    public abstract void OnDestory();
}