using BenchmarkDotNet.Attributes;
using LPS.Core.Rpc.RpcProperty;

namespace LPS.BenchMark;

[RpcCostumePropertyContainer]
internal class CostumeRpcContainerProperty2 : RpcPropertyContainer
{
    [RpcCostumePropertyAttribute]
    private readonly RpcPropertyContainer<float> subFloatProperty_ = 0.0f;

    public float SubFloatProperty
    {
        get => subFloatProperty_.Value;
        set => subFloatProperty_.Value = value;
    }
}
    
[RpcCostumePropertyContainer]
internal class CostumeRpcContainerProperty1 : RpcPropertyContainer
{
    [RpcCostumePropertyAttribute]
    public readonly RpcList<string> SubListProperty = new();
    [RpcCostumePropertyAttribute]
    public readonly CostumeRpcContainerProperty2 SubCostumerContainerRpcContainerProperty = new();
}

internal class NativeProperty2
{
    public float SubFloatProperty { get; set; } = 0.0f;
}

internal class NativeProperty1
{
    public readonly List<string> SubListProperty = new();
    public readonly NativeProperty2 SubCostumerContainerRpcContainerProperty = new();
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class NativePropertyVsLpsRpcProperty
{
    private readonly RpcComplexProperty<CostumeRpcContainerProperty1> rpcProp_;
    private readonly NativeProperty1 rpcNativeProp_;

    [Params(1, 10, 100, 1000, 10000)]
    public int AddCount;
    
    public NativePropertyVsLpsRpcProperty()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty1();
        rpcProp_ =
            new("test_costume_rpc_prop", RpcPropertySetting.FastSync, costumeRpcContainerProp);

        rpcNativeProp_ = new ();
    }
    
    [Benchmark]
    public void LpsRpcProperty()
    {
        // for (int i = 0; i < AddCount; ++i)
        // {
        //     rpcProp_.Val.SubListProperty.Add($"{i}");
        // }
        for (int i = 0; i < AddCount; ++i)
        {
            rpcProp_.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty = i;
        }
    }

    [Benchmark]
    public void NativeProperty()
    {
        // for (int i = 0; i < AddCount; ++i)
        // {
        //     rpcNativeProp_.SubListProperty.Add($"{i}");
        // }
        for (int i = 0; i < AddCount; ++i)
        {
            rpcNativeProp_.SubCostumerContainerRpcContainerProperty.SubFloatProperty = i;   
        }
    }
}
