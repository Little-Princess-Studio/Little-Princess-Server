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
    public readonly CostumeRpcContainerProperty2 SubCostumerContainerRpcContainerProperty = new();
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class NativePropertyVsLpsRpcProperty
{
    private readonly RpcComplexProperty<CostumeRpcContainerProperty1> rpcProp_;
    private readonly CostumeRpcContainerProperty1 rpcNativeProp_;
    private int counter1_;
    private int counter2_;

    public NativePropertyVsLpsRpcProperty()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty1();
        rpcProp_ =
            new("test_costume_rpc_prop", RpcPropertySetting.FastSync, costumeRpcContainerProp);

        rpcNativeProp_ = new CostumeRpcContainerProperty1();
    }
    
    [Benchmark]
    public void LpsRpcProperty()
    {
        ++counter1_;
        rpcProp_.Val.SubListProperty.Add($"{counter1_}");
        rpcProp_.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty = counter1_;
    }

    [Benchmark]
    public void NativeProperty()
    {
        ++counter2_; 
        rpcNativeProp_.SubListProperty.Add($"{counter2_}");
        rpcNativeProp_.SubCostumerContainerRpcContainerProperty.SubFloatProperty = counter2_;
    }
}
