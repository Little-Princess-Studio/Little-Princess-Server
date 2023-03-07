// -----------------------------------------------------------------------
// <copyright file="CostumeRpcContainerProperty2.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.BenchMark;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.RpcProperty;

/// <summary>
/// Costume rpc container property for benchmark.
/// </summary>
[RpcPropertyContainer]
internal class CostumeRpcContainerProperty2 : RpcPropertyCostumeContainer<CostumeRpcContainerProperty2>
{
    [RpcProperty]
    private readonly RpcPropertyContainer<float> subFloatProperty = 0.0f;

    /// <summary>
    /// Gets or sets the sub float property value.
    /// </summary>
    public float SubFloatProperty
    {
        get => this.subFloatProperty.Value;
        set => this.subFloatProperty.Value = value;
    }

    /// <inheritdoc/>
    public override Any ToRpcArg()
    {
        throw new NotImplementedException();
    }
}