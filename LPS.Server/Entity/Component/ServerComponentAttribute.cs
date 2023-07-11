// -----------------------------------------------------------------------
// <copyright file="ServerComponentAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity.Component;

using System;

/// <summary>
/// Represents an attribute that is used to mark a class as a component.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ServerComponentAttribute : Attribute
{
    /// <summary>
    /// The type of the component.
    /// </summary>
    public readonly Type ComponentType;

    /// <summary>
    /// Gets or Sets the name of the component in the property tree of the entity.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the component should be lazily loaded.
    /// </summary>
    public bool LazyLoad { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerComponentAttribute"/> class.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    public ServerComponentAttribute(Type componentType)
    {
        this.ComponentType = componentType;
    }
}