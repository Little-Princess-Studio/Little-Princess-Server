using LPS.Core.Rpc.RpcProperty;
using Xunit;

namespace LPS.UnitTest;

public class RpcPropertyUnitTest
{
    public class CostumeRpcProperty
    {
        
    }
    
    public class CostumeRpcContainerProperty : RpcPropertyContainer
    {
        
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
        
    }
}