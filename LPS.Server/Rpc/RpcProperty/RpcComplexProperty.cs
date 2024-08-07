﻿// -----------------------------------------------------------------------
// <copyright file="RpcComplexProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Rpc.RpcProperty;

using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Rpc complex property.
/// </summary>
/// <typeparam name="T">Type of the container's content which must be a RpcPropertyContainer.</typeparam>
public class RpcComplexProperty<T> : RpcComplexPropertyBase<T>
    where T : RpcPropertyContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcComplexProperty{T}"/> class.
    /// </summary>
    /// <param name="value">Initial value of the property.</param>
    public RpcComplexProperty(T value)
        : base(value)
    {
    }
}