// -----------------------------------------------------------------------
// <copyright file="RpcWrappedPropertyAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Attribute used to mark a property as a wrapped RPC property.
/// </summary>
[System.AttributeUsage(AttributeTargets.Property)]
public class RpcWrappedPropertyAttribute : System.Attribute
{
    /// <summary>
    /// Name of the property in the property tree.
    /// </summary>
    public readonly string? Name;

    /// <summary>
    /// Property settings.
    /// </summary>
    public readonly RpcPropertySetting Setting;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcWrappedPropertyAttribute"/> class.
    /// </summary>
    /// <param name="name">Name of the property in the property tree.</param>
    /// <param name="setting">Property setting.</param>
    public RpcWrappedPropertyAttribute(string name, RpcPropertySetting setting = RpcPropertySetting.None)
    {
        this.Name = name;
        this.Setting = setting;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcWrappedPropertyAttribute"/> class.
    /// </summary>
    public RpcWrappedPropertyAttribute()
    {
    }
}
