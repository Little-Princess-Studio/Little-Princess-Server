// -----------------------------------------------------------------------
// <copyright file="EntityClassAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.Attribute;

/// <summary>
/// Tag a class as entity class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class EntityClassAttribute : System.Attribute
{
    /// <summary>
    /// Name of the entity class.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityClassAttribute"/> class.
    /// </summary>
    /// <param name="name">Name of the entity class.</param>
    public EntityClassAttribute(string name = "")
    {
        this.Name = name;
    }
}