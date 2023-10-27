// -----------------------------------------------------------------------
// <copyright file="IValueSetable.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Defines a method to set the value of an object.
/// </summary>
internal interface IValueSetable
{
    /// <summary>
    /// Sets the value of an object.
    /// </summary>
    /// <param name="value">The value to set.</param>
    void SetValue(object value);
}