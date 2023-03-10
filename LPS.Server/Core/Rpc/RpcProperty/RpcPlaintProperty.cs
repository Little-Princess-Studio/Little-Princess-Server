// -----------------------------------------------------------------------
// <copyright file="RpcPlaintProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core.Rpc.RpcProperty;

using LPS.Common.Core.Rpc.RpcProperty;

/// <summary>
/// Rpc plaint property.
/// </summary>
/// <typeparam name="T">Type of the container's content which must be one of string/int/float/bool.</typeparam>
public class RpcPlaintProperty<T> : RpcPlaintPropertyBase<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPlaintProperty{T}"/> class.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="setting">Property Rpc setting, <see cref="RpcPropertySetting"/>.</param>
    /// <param name="value">Initial value of the property.</param>
    public RpcPlaintProperty(string name, RpcPropertySetting setting, T value)
        : base(name, setting, value)
    {
    }
}