// -----------------------------------------------------------------------
// <copyright file="RpcShadowComplexProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc.RpcProperty;

using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Rpc shadow complex property.
/// </summary>
/// <typeparam name="T">Type of the raw value.</typeparam>
public class RpcShadowComplexProperty<T> : RpcComplexPropertyBase<T>
    where T : RpcPropertyContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcShadowComplexProperty{T}"/> class.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    public RpcShadowComplexProperty(string name)
        : base(name, RpcPropertySetting.Shadow, null!)
    {
    }
}