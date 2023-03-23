// -----------------------------------------------------------------------
// <copyright file="RpcComplexPropertyBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

/// <summary>
/// Base class for RpcComplexProperty. RpcComplexPropertyBase should only contain
/// the RpcPropertyContainer's sub type value as the raw value.
/// </summary>
/// <typeparam name="T">Type of the raw value.</typeparam>
public class RpcComplexPropertyBase<T> : RpcProperty
    where T : RpcPropertyContainer
{
    /// <summary>
    /// Gets or sets the raw value of the property.
    /// </summary>
    public T Val
    {
        get => this.Get();
        set => this.Set(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcComplexPropertyBase{T}"/> class.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="setting">Setting of the property.</param>
    /// <param name="value">Raw value of the property.</param>
    public RpcComplexPropertyBase(string name, RpcPropertySetting setting, T value)
        : base(name, setting, value)
    {
        if (!this.IsShadowProperty)
        {
            this.Value.Name = name;
            this.Value.IsReferred = true;

            this.Value.UpdateTopOwner(this);
        }
    }

    public static implicit operator T(RpcComplexPropertyBase<T> complex) => complex.Val;

    /// <inheritdoc/>
    public override Any ToProtobuf()
    {
        return this.Val.ToRpcArg();
    }

    /// <inheritdoc/>
    public override void FromProtobuf(Any content)
    {
        if (!this.IsShadowProperty)
        {
            this.Val.RemoveFromPropTree();
        }
        else
        {
            // for shadow property, generic rpc container may not be registered
            if (!RpcHelper.IsRpcContainerRegistered(typeof(T)))
            {
                RpcHelper.RegisterRpcPropertyContainer(typeof(T));
            }
        }

        this.Value = (T)RpcHelper.CreateRpcPropertyContainerByType(typeof(T), content);
        this.Val.InsertToPropTree(null, this.Name, this);
    }

    private void Set(T value)
    {
        if (this.IsShadowProperty)
        {
            throw new Exception("Shadow property cannot be modified manually");
        }

        var old = this.Value;

        old.RemoveFromPropTree();
        old.InsertToPropTree(null, this.Name, this);

        this.Value = value;
        var path = new List<string> { this.Name };
        this.OnNotify(RpcPropertySyncOperation.SetValue, path, value, RpcSyncPropertyType.PlaintAndCostume);
    }

    private T Get()
    {
        return (T)this.Value;
    }
}