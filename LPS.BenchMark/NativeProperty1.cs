// -----------------------------------------------------------------------
// <copyright file="NativeProperty1.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.BenchMark;

/// <summary>
/// Native property for benchmark.
/// </summary>
internal class NativeProperty1
{
    /// <summary>
    /// Gets the sub Rpc list property.
    /// </summary>
    public readonly List<string> SubListProperty = new();

    /// <summary>
    /// Gets the sub costume container rpc container property.
    /// </summary>
    public readonly NativeProperty2 SubCostumeContainerRpcContainerProperty = new();
}