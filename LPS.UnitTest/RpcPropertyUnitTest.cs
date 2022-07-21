using Google.Protobuf.WellKnownTypes;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;
using Xunit;

namespace LPS.UnitTest;

public class RpcPropertyUnitTest
{
    [RpcPropertyContainer]
    private class CostumeRpcContainerProperty2 : RpcPropertyCostumeContainer
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
    private class CostumeRpcContainerProperty1 : RpcPropertyCostumeContainer
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

        public readonly RpcPlainProperty<string> TestRpcPlainPropStr =
            new(nameof(TestEntity.TestRpcPlainPropStr),
                RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, "");

        public readonly RpcComplexProperty<CostumeRpcContainerProperty1> TestCostumeRpcContainerProperty1
            = new(nameof(TestCostumeRpcContainerProperty1),
                RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new());

        public readonly RpcComplexProperty<CostumeRpcContainerProperty2> TestCostumeRpcContainerProperty2
            = new(nameof(TestCostumeRpcContainerProperty2),
                RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new());
    }

    private class TestShadowEntity : ShadowEntity
    {
        public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp =
            new(nameof(TestShadowEntity.TestRpcProp));

        public readonly RpcShadowPlaintProperty<string> TestRpcPlainPropStr =
            new(nameof(TestShadowEntity.TestRpcPlainPropStr));

        public readonly RpcShadowComplexProperty<CostumeRpcContainerProperty1> TestCostumeRpcContainerProperty1 =
            new(nameof(TestCostumeRpcContainerProperty1));

        public readonly RpcShadowComplexProperty<CostumeRpcContainerProperty2> TestCostumeRpcContainerProperty2 =
            new(nameof(TestCostumeRpcContainerProperty2));
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
        RpcPlainProperty<string> rpcPlainStrProp = new("test_str_prop", RpcPropertySetting.Permanent, "");
        rpcPlainStrProp.Val = "321";
        Assert.True(rpcPlainStrProp.Val == "321");
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

        RpcComplexProperty<RpcDictionary<string, RpcDictionary<int, RpcList<int>>>> rpcProp =
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
        RpcComplexProperty<CostumeRpcContainerProperty1> rpcProp =
            new("test_costume_rpc_prop", RpcPropertySetting.FastSync, costumeRpcContainerProp);

        rpcProp.Val.SubListProperty.Add("111");

        CostumeRpcContainerProperty1 cprop = rpcProp;
        cprop.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 1.0f;

        Assert.True(costumeRpcContainerProp.IsReferred);
        Assert.True(costumeRpcContainerProp.SubCostumerContainerRpcContainerProperty.IsReferred);
        Assert.Equal("111", rpcProp.Val.SubListProperty[0]);
        Assert.Equal(1.0f, rpcProp.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty);
    }

    [Fact]
    public void TestPropertySerialization()
    {
        RpcHelper.ScanRpcPropertyContainer("LPS.UnitTest");

        var entity = new TestEntity();
        var shadowEntity = new TestShadowEntity();
        RpcHelper.BuildPropertyTree(entity);
        RpcHelper.BuildPropertyTree(shadowEntity);

        entity.TestRpcProp.Val.Add("1");
        entity.TestRpcProp.Val.Add("2");
        entity.TestRpcProp.Val.Add("3");

        entity.TestRpcPlainPropStr.Val = "hello, LPS";

        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("a");
        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("b");
        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("c");

        entity.TestCostumeRpcContainerProperty1.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 100.0f;

        entity.TestCostumeRpcContainerProperty2.Val.SubFloatProperty = 200.0f;

        entity.FullSync((id, content) => { shadowEntity.FromSyncContent(content); });

        Assert.Equal("1", shadowEntity.TestRpcProp.Val[0]);
        Assert.Equal("2", shadowEntity.TestRpcProp.Val[1]);
        Assert.Equal("3", shadowEntity.TestRpcProp.Val[2]);

        Assert.Equal("hello, LPS", shadowEntity.TestRpcPlainPropStr);

        Assert.Equal("a", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[0]);
        Assert.Equal("b", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[1]);
        Assert.Equal("c", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[2]);

        Assert.Equal(100.0f,
            shadowEntity.TestCostumeRpcContainerProperty1.Val.SubCostumerContainerRpcContainerProperty
                .SubFloatProperty);

        Assert.Equal(200.0f,
            shadowEntity.TestCostumeRpcContainerProperty2.Val.SubFloatProperty);
    }
}