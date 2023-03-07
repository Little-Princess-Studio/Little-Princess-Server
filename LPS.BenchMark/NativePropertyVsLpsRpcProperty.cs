// -----------------------------------------------------------------------
// <copyright file="NativePropertyVsLpsRpcProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.BenchMark;

using BenchmarkDotNet.Attributes;
using LPS.Common.Core.Rpc.RpcProperty;

/// <summary>
/// Benchmark class.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class NativePropertyVsLpsRpcProperty
{
    private readonly RpcComplexPropertyBase<CostumeRpcContainerProperty1> rpcProp;
    private readonly NativeProperty1 rpcNativeProp;

    [Params(1, 10, 100, 1000, 10000)]
    private int addCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativePropertyVsLpsRpcProperty"/> class.
    /// </summary>
    public NativePropertyVsLpsRpcProperty()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty1();
        this.rpcProp =
            new(
                "test_costume_rpc_prop",
                RpcPropertySetting.FastSync,
                costumeRpcContainerProp);

        this.rpcNativeProp = new();
    }

    /// <summary>
    /// Benchmark for RpcProperty.
    /// </summary>
    [Benchmark]
    public void LpsRpcProperty()
    {
        // for (int i = 0; i < AddCount; ++i)
        // {
        //     rpcProp_.Val.SubListProperty.Add($"{i}");
        // }
        for (int i = 0; i < this.addCount; ++i)
        {
            this.rpcProp.Val.SubCostumeContainerRpcContainerProperty.SubFloatProperty = i;
        }
    }

    /// <summary>
    /// Bechmark for native property.
    /// </summary>
    [Benchmark]
    public void NativeProperty()
    {
        // for (int i = 0; i < AddCount; ++i)
        // {
        //     rpcNativeProp_.SubListProperty.Add($"{i}");
        // }
        for (int i = 0; i < this.addCount; ++i)
        {
            this.rpcNativeProp.SubCostumeContainerRpcContainerProperty.SubFloatProperty = i;
        }
    }
}