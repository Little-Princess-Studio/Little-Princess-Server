// -----------------------------------------------------------------------
// <copyright file="IPropertyTree.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty.Weaving;

/// <summary>
/// Represents a class that provides a property to detect whether the property tree is built.
/// </summary>
internal interface IPropertyTree
{
    /// <summary>
    /// Gets a value indicating whether the property tree is built.
    /// </summary>
    bool IsPropertyTreeBuilt { get; }

    /// <summary>
    /// Gets the IValueGetable container with the specified name from the entity's property tree.
    /// </summary>
    /// <param name="name">The name of the IValueGetable container to retrieve.</param>
    /// <returns>The IValueGetable container with the specified name.</returns>
    IValueGetable GetGetableContainer(string name);

    /// <summary>
    /// Gets the IValueGetable container with the specified name from the entity's property tree.
    /// </summary>
    /// <param name="name">The name of the IValueSetable container to retrieve.</param>
    /// <returns>The IValueSetable container with the specified name.</returns>
    IValueSetable GetSetableContainer(string name);
}