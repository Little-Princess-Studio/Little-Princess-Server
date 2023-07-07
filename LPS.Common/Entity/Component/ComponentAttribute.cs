// -----------------------------------------------------------------------
// <copyright file="ComponentAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

/// <summary>
/// Represents an attribute that is used to mark a class as a component.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ComponentAttribute : Attribute
{
    /// <summary>
    /// The type of the component.
    /// </summary>
    public readonly Type ComponentType;

    /// <summary>
    /// The name of the component in the property tree of the entity.
    /// </summary>
    public readonly string PropertyName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentAttribute"/> class.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    /// <param name="propertyName">The name of the component in the property tree of the entity.</param>
    public ComponentAttribute(Type componentType, string propertyName)
    {
        this.ComponentType = componentType;
        this.PropertyName = propertyName;
    }
}