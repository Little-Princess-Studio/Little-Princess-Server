﻿// -----------------------------------------------------------------------
// <copyright file="RpcPlaintPropertyBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Rpc.RpcProperty;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Debug;
using LPS.Common.Core.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Core.Rpc.RpcPropertySync;

/// <summary>
/// Base class for RpcPlaintProperty. RpcPlaintProperty should only contain the plaint type value as the raw value.
/// Plaint type should be one of follows:
/// 1. string
/// 2. int
/// 3. float
/// 4. bool
/// 5. Mailbox.
/// </summary>
/// <typeparam name="T">Type of the raw value.</typeparam>
public abstract class RpcPlaintPropertyBase<T> : RpcProperty
{
    static RpcPlaintPropertyBase()
    {
        RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPlaintPropertyBase{T}"/> class.
    /// </summary>
    /// <param name="name">Nmae of the property.</param>
    /// <param name="setting">Setting of the property.</param>
    /// <param name="value">Value of the property.</param>
    protected RpcPlaintPropertyBase(string name, RpcPropertySetting setting, T value)
        : base(name, setting, new RpcPropertyContainer<T>(value))
    {
        this.Value.Name = name;
    }

    private RpcPlaintPropertyBase()
        : base(string.Empty, RpcPropertySetting.None, null!)
    {
        throw new Exception("Invalid RpcPlaintProperty Construction.");
    }

    /// <summary>
    /// Gets or sets the raw value of the property.
    /// </summary>
    public T Val
    {
        get => this.Get();
        set => this.Set(value);
    }

    public static implicit operator T(RpcPlaintPropertyBase<T> container) => container.Val;

    /// <inheritdoc/>
    public override Any ToProtobuf()
    {
        return Any.Pack(RpcHelper.RpcArgToProtobuf(this.Val));
    }

    /// <inheritdoc/>
    public override void FromProtobuf(Any content)
    {
        this.Value =
            (RpcPropertyContainer<T>)RpcHelper.CreateRpcPropertyContainerByType(
                typeof(RpcPropertyContainer<T>),
                content);
        this.Value.Name = this.Name;
        this.Value.IsReferred = true;
    }

    private void Set(T value)
    {
        if (this.IsShadowProperty)
        {
            throw new Exception("Shadow property cannot be modified manually");
        }

        // var old = ((RpcPropertyContainer<T>) this.Value).Value;
        ((RpcPropertyContainer<T>)this.Value).Value = value;

        var path = new List<string> { this.Name };

        Logger.Debug($"[Plaint Set] {value}");
        this.OnNotify(RpcPropertySyncOperation.SetValue, path, this.Value, RpcSyncPropertyType.PlaintAndCostume);
    }

    private T Get()
    {
        return (RpcPropertyContainer<T>)this.Value;
    }
}