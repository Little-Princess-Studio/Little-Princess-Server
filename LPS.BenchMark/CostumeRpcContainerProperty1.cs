// -----------------------------------------------------------------------
// <copyright file="CostumeRpcContainerProperty1.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.BenchMark;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Costume rpc container property for benchmark.
/// </summary>
[RpcPropertyContainer]
internal class CostumeRpcContainerProperty1 : RpcPropertyCostumeContainer<CostumeRpcContainerProperty1>
{
    /// <summary>
    /// Gets the sub Rpc list property.
    /// </summary>
    [RpcProperty]
    public readonly RpcList<string> SubListProperty = new();

    /// <summary>
    /// Gets the sub costume container rpc container property.
    /// </summary>
    [RpcProperty]
    public readonly CostumeRpcContainerProperty2 SubCostumeContainerRpcContainerProperty = new();

    /// <inheritdoc/>
    public override Any ToRpcArg()
    {
        throw new NotImplementedException();
    }
}