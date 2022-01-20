using LPS.Core.Rpc.RpcProperty;
using Xunit;

namespace LPS.UnitTest;

public class RpcPropertyUnitTest
{
    [RpcCostumePropertyContainerAttribute]
    private class CostumeRpcContainerProperty : RpcPropertyContainer
    {
        [RpcCostumePropertyAttribute]
        public readonly RpcList<string> SubListProperty = new();
        [RpcCostumePropertyAttribute]
        public readonly RpcPropertyContainer<float> SubFloatProperty = new(0.0f);
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
        rpcPlainStrProp.Set("321");
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

        Assert.False(rpcList.Reffered);
        Assert.True(rpcList2.Reffered);
        Assert.Equal(333, rpcProp.Val["n1"][123][0]);
    }

    [Fact]
    public void TestCostumeRpcProp()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty();
        RpcComplexProperty<CostumeRpcContainerProperty> rpcProp =
            new("test_costume_rpc_prop", RpcPropertySetting.FastSync, costumeRpcContainerProp);

        rpcProp.Val.SubListProperty.Add("111");
        
        CostumeRpcContainerProperty cprop = rpcProp;
        cprop.SubFloatProperty.Value = 1.0f;
        
        Assert.True(costumeRpcContainerProp.Reffered);
        Assert.Equal("111", rpcProp.Val.SubListProperty[0]);
        Assert.Equal(1.0f, rpcProp.Val.SubFloatProperty.Value);
    }
}