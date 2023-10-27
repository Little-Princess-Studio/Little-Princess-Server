// -----------------------------------------------------------------------
// <copyright file="IValueGetable.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Interface for objects that can return their value.
/// </summary>
internal interface IValueGetable
{
    /// <summary>
    /// Gets the value of the property.
    /// </summary>
    /// <returns>The value of the property.</returns>
    object GetValue();
}