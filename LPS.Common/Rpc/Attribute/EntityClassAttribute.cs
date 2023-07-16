// -----------------------------------------------------------------------
// <copyright file="EntityClassAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

/// <summary>
/// Tag a class as entity class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class EntityClassAttribute : System.Attribute
{
    /// <summary>
    /// Gets or sets name of the entity class.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets db collection name of the entity class.
    /// </summary>
    public string DbCollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether instances of the entity class should be saved to the database.
    /// </summary>
    public bool IsDatabaseEntity { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityClassAttribute"/> class.
    /// </summary>
    /// <param name="name">Name of the entity class.</param>
    /// <param name="isDatabaseEntity">Indicates whether instances of the entity class should be automatically saved/load from the database.</param>
}