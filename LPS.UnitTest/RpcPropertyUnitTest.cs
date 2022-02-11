using LPS.Core.Rpc.RpcProperty;
using Xunit;

namespace LPS.UnitTest;

public class RpcPropertyUnitTest
{
    [RpcCostumePropertyContainerAttribute]
    private class CostumeRpcContainerProperty2 : RpcPropertyContainer
    {
        [RpcCostumePropertyAttribute] 
        private readonly RpcPropertyContainer<float> subFloatProperty_ = 0.0f;

        public float SubFloatProperty
        {
            get => subFloatProperty_.Value;
            set => subFloatProperty_.Value = value;
        }
    }
    
    [RpcCostumePropertyContainerAttribute]
    private class CostumeRpcContainerProperty1 : RpcPropertyContainer
    {
        [RpcCostumePropertyAttribute]
        public readonly RpcList<string> SubListProperty = new();
        [RpcCostumePropertyAttribute]
        public readonly CostumeRpcContainerProperty2 SubCostumerContainerRpcContainerProperty = new();
    }

    [Fact]
    public void TestRpcList()
    {
        RpcComplexProperty<RpcList<string>> rpcProp = new ("test_list_prop", RpcPropertySetting.Permanent, new RpcList<string>());
        rpcProp.Val.Add("123");

        RpcList<string> rpcList = rpcProp;
        rpcList.Add("456");

        Assert.True(rpcProp.Val[0] == "123");
        Assert.True(rpcProp.Val[1] == "456");
    }

    [Fact]
    public void TestRpcString()
    {
        RpcPlainProperty<string> rpcPlainStrProp = new("test_str_prop", RpcPropertySetting.Permanent, "");
        rpcPlainStrProp.Val = "321";
        Assert.True(rpcPlainStrProp.Val == "321");
    }

    [Fact]
    public void TestRpcDict()
    {
        RpcComplexProperty<RpcDictionary<string, int>> rpcProp = new("test_dict_prop", RpcPropertySetting.ClientOwn, new RpcDictionary<string, int>());
        rpcProp.Val["test_key_1"] = 123;

        RpcDictionary<string, int> rpcDict = rpcProp;
        Assert.True(rpcDict["test_key_1"] == 123);

        rpcDict["test_key_2"] = 321;
        Assert.True(rpcDict["test_key_2"] == 321);
    }

    [Fact]
    public void TestRpcComplexDict()
    {
        var rpcList = new RpcList<int>();
        var rpcList2 = new RpcList<int>();

        RpcComplexProperty<RpcDictionary<string, RpcDictionary<int, RpcList<int>>>> rpcProp =
            new("test_dict_prop", RpcPropertySetting.ClientOwn, new())
                {
                    Val =
                    {
                        ["n1"] = new RpcDictionary<int, RpcList<int>>
                        {
                            [123] = rpcList,
                        }
                    }
                };

        rpcProp.Val["n1"][123] = rpcList2;
        rpcProp.Val["n1"][123].Add(333);

        Assert.False(rpcList.IsReffered);
        Assert.True(rpcList2.IsReffered);
        Assert.Equal(333, rpcProp.Val["n1"][123][0]);
    }

    [Fact]
    public void TestCostumeRpcProp()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty1();
        RpcComplexProperty<CostumeRpcContainerProperty1> rpcProp =
            new("test_costume_rpc_prop", RpcPropertySetting.FastSync, costumeRpcContainerProp);

        rpcProp.Val.SubListProperty.Add("111");
        
        CostumeRpcContainerProperty1 cprop = rpcProp;
        cprop.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 1.0f;
        
        Assert.True(costumeRpcContainerProp.IsReffered);
        Assert.True(costumeRpcContainerProp.SubCostumerContainerRpcContainerProperty.IsReffered);
        Assert.Equal("111", rpcProp.Val.SubListProperty[0]);
        Assert.Equal(1.0f, rpcProp.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty);
    }
}