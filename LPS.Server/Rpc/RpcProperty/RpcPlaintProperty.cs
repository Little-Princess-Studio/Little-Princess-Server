// -----------------------------------------------------------------------
// <copyright file="RpcPlaintProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc.RpcProperty;

using LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Rpc plaint property.
/// </summary>
/// <typeparam name="T">Type of the container's content which must be one of string/int/float/bool.</typeparam>
public class RpcPlaintProperty<T> : RpcPlaintPropertyBase<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPlaintProperty{T}"/> class.
    /// </summary>
    /// <param name="value">Initial value of the property.</param>
    public RpcPlaintProperty(T value)
        : base(value)
    {
    }
}