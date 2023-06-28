﻿// -----------------------------------------------------------------------
// <copyright file="RpcShadowPlaintProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc.RpcProperty;

using LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Rpc shadow plaint property.
/// </summary>
/// <typeparam name="T">Type of the raw value.</typeparam>
public class RpcShadowPlaintProperty<T> : RpcPlaintPropertyBase<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcShadowPlaintProperty{T}"/> class.
    /// </summary>
    public RpcShadowPlaintProperty()
        : base(default(T)!)
    {
    }

    /// <summary>
    /// Init shadow property name.
    /// </summary>
    /// <param name="name">Name of the shadow property.</param>
    public void Init(string name)
    {
        base.Init(name, RpcPropertySetting.Shadow);
    }

    /// <inheritdoc/>
    public override void Init(string name, RpcPropertySetting setting)
    {
        base.Init(name, RpcPropertySetting.Shadow);
    }
}