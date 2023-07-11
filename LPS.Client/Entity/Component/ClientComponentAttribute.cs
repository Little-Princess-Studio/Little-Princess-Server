// -----------------------------------------------------------------------
// <copyright file="ClientComponentAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Entity.Component;

/// <summary>
/// Represents an attribute that is used to mark a class as a component.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ClientComponentAttribute : Attribute
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
    /// Initializes a new instance of the <see cref="ClientComponentAttribute"/> class.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    public ClientComponentAttribute(Type componentType)
    {
        this.ComponentType = componentType;
    }
}