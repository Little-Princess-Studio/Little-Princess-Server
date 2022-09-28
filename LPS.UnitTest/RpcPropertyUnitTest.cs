using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using LPS.Client;
using LPS.Client.Core.Rpc.RpcProperty;
using LPS.Common.Core.Entity;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Server.Core.Entity;
using LPS.Server.Core.Rpc;
using LPS.Server.Core.Rpc.RpcProperty;
using Xunit;

namespace LPS.UnitTest;

public class RpcPropertyUnitTest
{
    [RpcPropertyContainer]
    private class CostumeRpcContainerProperty2 : RpcPropertyCostumeContainer<CostumeRpcContainerProperty2>
    {
        [RpcProperty] private readonly RpcPropertyContainer<float> subFloatProperty_ = 0.0f;

        public float SubFloatProperty
        {
            get => subFloatProperty_.Value;
            set => subFloatProperty_.Value = value;
        }

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer DeserializeStatic(Any content) =>
            CreateSerializedContainer<CostumeRpcContainerProperty2>(content);
    }

    [RpcPropertyContainer]
    private class CostumeRpcContainerProperty1 : RpcPropertyCostumeContainer<CostumeRpcContainerProperty1>
    {
        [RpcProperty] public readonly RpcList<string> SubListProperty = new();
        [RpcProperty] public readonly CostumeRpcContainerProperty2 SubCostumerContainerRpcContainerProperty = new();

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer DeserializeStatic(Any content) =>
            CreateSerializedContainer<CostumeRpcContainerProperty1>(content);
    }

    private class TestEntity : DistributeEntity
    {
        public readonly RpcComplexProperty<RpcList<string>> TestRpcProp =
            new(nameof(TestEntity.TestRpcProp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow,
                new RpcList<string>());

        public readonly RpcPlaintProperty<string> TestRpcPlaintPropStr =
            new(nameof(TestEntity.TestRpcPlaintPropStr),
                RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, "");

        public readonly RpcComplexProperty<CostumeRpcContainerProperty1> TestCostumeRpcContainerProperty1
            = new(nameof(TestCostumeRpcContainerProperty1),
                RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new());

        public readonly RpcComplexProperty<CostumeRpcContainerProperty2> TestCostumeRpcContainerProperty2
            = new(nameof(TestCostumeRpcContainerProperty2),
                RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new());

        public readonly RpcComplexProperty<RpcDictionary<string, RpcList<int>>> TestComplexRpcProp =
            new(nameof(TestComplexRpcProp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new());
    }

    private class TestShadowEntity : ShadowEntity
    {
        public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp =
            new((string) nameof(TestRpcProp));

        public readonly RpcShadowPlaintProperty<string> TestRpcPlaintPropStr =
            new((string) nameof(TestRpcPlaintPropStr));

        public readonly RpcShadowComplexProperty<CostumeRpcContainerProperty1> TestCostumeRpcContainerProperty1 =
            new((string) nameof(TestCostumeRpcContainerProperty1));

        public readonly RpcShadowComplexProperty<CostumeRpcContainerProperty2> TestCostumeRpcContainerProperty2 =
            new((string) nameof(TestCostumeRpcContainerProperty2));
    }

    public RpcPropertyUnitTest()
    {
        RpcHelper.ScanRpcPropertyContainer("LPS.UnitTest");
    }

    [Fact]
    public void TestRpcList()
    {
        RpcComplexProperty<RpcList<string>> rpcProp = new("test_list_prop", RpcPropertySetting.Permanent,
            new RpcList<string>());
        rpcProp.Val.Add("123");

        RpcList<string> rpcList = rpcProp;
        rpcList.Add("456");

        Assert.True(rpcProp.Val[0] == "123");
        Assert.True(rpcProp.Val[1] == "456");
    }

    [Fact]
    public void TestRpcString()
    {
        RpcPlaintProperty<string> rpcPlaintStrProp = new("test_str_prop", RpcPropertySetting.Permanent, "");
        rpcPlaintStrProp.Val = "321";
        Assert.True(rpcPlaintStrProp.Val == "321");
    }

    [Fact]
    public void TestRpcDict()
    {
        RpcComplexProperty<RpcDictionary<string, int>> rpcProp = new("test_dict_prop", RpcPropertySetting.ServerOnly,
            new RpcDictionary<string, int>());
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

        RpcComplexPropertyBase<RpcDictionary<string, RpcDictionary<int, RpcList<int>>>> rpcProp =
            new("test_dict_prop", RpcPropertySetting.ServerOnly, new())
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

        Assert.False(rpcList.IsReferred);
        Assert.True(rpcList2.IsReferred);
        Assert.Equal(333, rpcProp.Val["n1"][123][0]);
    }

    [Fact]
    public void TestCostumeRpcProp()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty1();
        RpcComplexPropertyBase<CostumeRpcContainerProperty1> rpcProp =
            new("test_costume_rpc_prop", RpcPropertySetting.FastSync, costumeRpcContainerProp);

        rpcProp.Val.SubListProperty.Add("111");

        CostumeRpcContainerProperty1 cprop = rpcProp;
        cprop.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 1.0f;

        Assert.True(costumeRpcContainerProp.IsReferred);
        Assert.True(costumeRpcContainerProp.SubCostumerContainerRpcContainerProperty.IsReferred);
        Assert.Equal("111", rpcProp.Val.SubListProperty[0]);
        Assert.Equal(1.0f, rpcProp.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty);
    }

    private bool CheckReferred(RpcPropertyContainer? container)
    {
        if (container == null)
        {
            return false;
        }

        foreach (var (_, value) in container.Children!)
        {
            if (!value.IsReferred)
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void TestPropertySerialization()
    {
        var entity = new TestEntity();
        var shadowEntity = new TestShadowEntity();
        RpcServerHelper.BuildPropertyTree(entity);
        RpcClientHelper.BuildPropertyTree(shadowEntity);

        entity.TestRpcProp.Val.Add("1");
        entity.TestRpcProp.Val.Add("2");
        entity.TestRpcProp.Val.Add("3");

        entity.TestRpcPlaintPropStr.Val = "hello, LPS";

        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("a");
        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("b");
        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("c");

        entity.TestCostumeRpcContainerProperty1.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 100.0f;

        entity.TestCostumeRpcContainerProperty2.Val.SubFloatProperty = 200.0f;

        entity.FullSync((id, content) => { shadowEntity.FromSyncContent(content); });

        Assert.Equal("1", shadowEntity.TestRpcProp.Val[0]);
        Assert.Equal("2", shadowEntity.TestRpcProp.Val[1]);
        Assert.Equal("3", shadowEntity.TestRpcProp.Val[2]);

        Assert.Equal("hello, LPS", shadowEntity.TestRpcPlaintPropStr);

        Assert.Equal("a", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[0]);
        Assert.Equal("b", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[1]);
        Assert.Equal("c", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[2]);

        Assert.Equal(100.0f,
            shadowEntity.TestCostumeRpcContainerProperty1.Val.SubCostumerContainerRpcContainerProperty
                .SubFloatProperty);

        Assert.Equal(200.0f,
            shadowEntity.TestCostumeRpcContainerProperty2.Val.SubFloatProperty);

        Assert.True(this.CheckReferred(shadowEntity.TestRpcProp.Val));
        Assert.True(this.CheckReferred(shadowEntity.TestCostumeRpcContainerProperty1.Val));
        Assert.True(this.CheckReferred(shadowEntity.TestCostumeRpcContainerProperty2.Val));
    }

    [Fact]
    public void TestListPropertyChangeNotification()
    {
        var entity = new TestEntity();
        var addElemCnt = 0;
        var clearCnt = 0;
        var updateCnt = 0;
        var removeCnt = 0;
        var setCnt = 0;

        entity.TestRpcProp.Val.OnAddElem = val =>
        {
            ++addElemCnt;
            Assert.Equal("111", val);
        };
        entity.TestRpcProp.Val.OnClear = () => { ++clearCnt; };
        entity.TestRpcProp.Val.OnUpdatePair = (key, val, newVal) =>
        {
            ++updateCnt;
            Assert.Equal(1, key);
            Assert.Equal("111", val);
            Assert.Equal("222", newVal);
        };
        entity.TestRpcProp.Val.OnRemoveElem = (key, val) =>
        {
            ++removeCnt;
            Assert.Equal(2, key);
            Assert.Equal("111", val);
        };
        entity.TestRpcProp.Val.OnSetValue = (val, newVal) =>
        {
            ++setCnt;
            Assert.Equal(new List<string> {"111", "222"}, val);
            Assert.Equal(new List<string> {"333", "333", "333"}, newVal);
        };

        entity.TestRpcProp.Val.Add("111");
        entity.TestRpcProp.Val.Add("111");
        entity.TestRpcProp.Val.Add("111");
        entity.TestRpcProp.Val[1] = "222";
        entity.TestRpcProp.Val.RemoveAt(2);
        entity.TestRpcProp.Val.Assign(new RpcList<string>(3, "333"));
        entity.TestRpcProp.Val.Clear();

        Assert.Equal(3, addElemCnt);
        Assert.Equal(1, setCnt);
        Assert.Equal(1, removeCnt);
        Assert.Equal(1, updateCnt);
        Assert.Equal(1, clearCnt);
    }

    [Fact]
    public void TestDictPropertyChangeNotification()
    {
        var entity = new TestEntity();
        var removeCnt = 0;
        var updateCnt = 0;
        var clearCnt = 0;
        
        entity.TestComplexRpcProp.Val.OnRemoveElem = (key, val) =>
        {
            ++removeCnt;
            Assert.Equal("key_1", key);
        };

        entity.TestComplexRpcProp.Val.OnUpdatePair = (key, val, newVal) =>
        {
            if (key is "key_1" or "key_2")
            {
                ++updateCnt;
            }
            Assert.Null(val);
        };

        entity.TestComplexRpcProp.Val.OnClear = () => ++clearCnt;

        entity.TestComplexRpcProp.Val.OnClear = () => ++clearCnt;
        entity.TestComplexRpcProp.Val["key_1"] = new RpcList<int>();
        entity.TestComplexRpcProp.Val["key_2"] = new RpcList<int>();
        entity.TestComplexRpcProp.Val.Remove("key_1");
        entity.TestComplexRpcProp.Val.Clear();

        Assert.Equal(1, removeCnt);
        Assert.Equal(2, updateCnt);
        Assert.Equal(1, clearCnt);
    }
}