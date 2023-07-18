// -----------------------------------------------------------------------
// <copyright file="ITypeIdSupport.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Util;

/// <summary>
/// Interface for types that support a unique identifier.
/// </summary>
public interface ITypeIdSupport
{
    /// <summary>
    /// Gets the unique identifier for the type.
    /// </summary>
    /// <returns>The unique identifier for the type.</returns>
    uint TypeId { get; }
}