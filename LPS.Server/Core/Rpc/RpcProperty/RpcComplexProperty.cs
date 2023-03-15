// -----------------------------------------------------------------------
// <copyright file="RpcComplexProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core.Rpc.RpcProperty;

using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Common.Core.Rpc.RpcProperty.RpcContainer;

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
    /// <param name="name">Name of the property.</param>
    /// <param name="setting">Property Rpc setting, <see cref="RpcPropertySetting"/>.</param>
    /// <param name="value">Initial value of the property.</param>
    public RpcComplexProperty(string name, RpcPropertySetting setting, T value)
        : base(name, setting, value)
    {
    }
}