// -----------------------------------------------------------------------
// <copyright file="RpcPropertyUnitTest.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.UnitTest;

using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using LPS.Client;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Entity;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.RpcProperty;
using Xunit;

/// <summary>
/// Unit test class for RpcProperty.
/// </summary>
public class RpcPropertyUnitTest
{
    [RpcPropertyContainer]
    private class CostumeRpcContainerProperty2 : RpcPropertyCostumeContainer<CostumeRpcContainerProperty2>
    {
        [RpcProperty]
        private readonly RpcPropertyContainer<float> subFloatProperty = 0.0f;

        public float SubFloatProperty
        {
            get => this.subFloatProperty.Value;
            set => this.subFloatProperty.Value = value;
        }

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer DeserializeStatic(Any content) =>
            CreateSerializedContainer<CostumeRpcContainerProperty2>(content);
    }

    [RpcPropertyContainer]
    private class CostumeRpcContainerProperty1 : RpcPropertyCostumeContainer<CostumeRpcContainerProperty1>
    {
        [RpcProperty]
        public readonly RpcList<string> SubListProperty = new();

        [RpcProperty]
        public readonly CostumeRpcContainerProperty2 SubCostumerContainerRpcContainerProperty = new();

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer DeserializeStatic(Any content) =>
            CreateSerializedContainer<CostumeRpcContainerProperty1>(content);
    }

    private class TestEntity : DistributeEntity
    {
        [RpcProperty(nameof(TestEntity.TestRpcProp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
        public readonly RpcComplexProperty<RpcList<string>> TestRpcProp = new(new RpcList<string>());

        [RpcProperty(
            nameof(TestEntity.TestRpcPlaintPropStr),
            RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
        public readonly RpcPlaintProperty<string> TestRpcPlaintPropStr = new(string.Empty);

        [RpcProperty(
            nameof(TestCostumeRpcContainerProperty1),
            RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
        public readonly RpcComplexProperty<CostumeRpcContainerProperty1>
            TestCostumeRpcContainerProperty1 = new(new());

        [RpcProperty(
            nameof(TestCostumeRpcContainerProperty2),
            RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
        public readonly RpcComplexProperty<CostumeRpcContainerProperty2> TestCostumeRpcContainerProperty2 = new(new());

        [RpcProperty(
            nameof(TestComplexRpcProp),
            RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
        public readonly RpcComplexProperty<RpcDictionary<string, RpcList<int>>> TestComplexRpcProp = new(new());
    }

    private class TestShadowEntity : ShadowEntity
    {
        [RpcProperty(nameof(TestRpcProp))]
        public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp = new();

        [RpcProperty(nameof(TestRpcPlaintPropStr))]
        public readonly RpcShadowPlaintProperty<string> TestRpcPlaintPropStr = new();

        [RpcProperty(nameof(TestCostumeRpcContainerProperty1))]
        public readonly RpcShadowComplexProperty<CostumeRpcContainerProperty1> TestCostumeRpcContainerProperty1 = new();

        [RpcProperty(nameof(TestCostumeRpcContainerProperty2))]
        public readonly RpcShadowComplexProperty<CostumeRpcContainerProperty2> TestCostumeRpcContainerProperty2 = new();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPropertyUnitTest"/> class.
    /// </summary>
    public RpcPropertyUnitTest()
    {
        RpcHelper.ScanRpcPropertyContainer("LPS.UnitTest");
    }

    /// <summary>
    /// Unit test for RpcList.
    /// </summary>
    [Fact]
    public void TestRpcList()
    {
        RpcComplexProperty<RpcList<string>> rpcProp = new(new RpcList<string>());
        rpcProp.Init("test_list_prop", RpcPropertySetting.Permanent);
        rpcProp.Val.Add("123");

        RpcList<string> rpcList = rpcProp;
        rpcList.Add("456");

        Assert.True(rpcProp.Val[0] == "123");
        Assert.True(rpcProp.Val[1] == "456");
    }

    /// <summary>
    /// Unit test for RpcString.
    /// </summary>
    [Fact]
    public void TestRpcString()
    {
        RpcPlaintProperty<string> rpcPlaintStrProp = new(string.Empty);
        rpcPlaintStrProp.Init("test_str_prop", RpcPropertySetting.Permanent);
        rpcPlaintStrProp.Val = "321";
        Assert.True(rpcPlaintStrProp.Val == "321");
    }

    /// <summary>
    /// Unit test for RpcDict.
    /// </summary>
    [Fact]
    public void TestRpcDict()
    {
        RpcComplexProperty<RpcDictionary<string, int>> rpcProp = new(new RpcDictionary<string, int>());
        rpcProp.Init("test_dict_prop", RpcPropertySetting.ServerOnly);
        rpcProp.Val["test_key_1"] = 123;

        RpcDictionary<string, int> rpcDict = rpcProp;
        Assert.True(rpcDict["test_key_1"] == 123);

        rpcDict["test_key_2"] = 321;
        Assert.True(rpcDict["test_key_2"] == 321);
    }

    /// <summary>
    /// Unit test for Rpc complex dict.
    /// </summary>
    [Fact]
    public void TestRpcComplexDict()
    {
        var rpcList = new RpcList<int>();
        var rpcList2 = new RpcList<int>();

        RpcComplexProperty<RpcDictionary<string, RpcDictionary<int, RpcList<int>>>> rpcProp =
            new(new())
            {
                Val =
                {
                    ["n1"] = new RpcDictionary<int, RpcList<int>>
                    {
                        [123] = rpcList,
                    },
                },
            };

        rpcProp.Init("test_dict_prop", RpcPropertySetting.ServerOnly);

        rpcProp.Val["n1"][123] = rpcList2;
        rpcProp.Val["n1"][123].Add(333);

        Assert.False(rpcList.IsReferred);
        Assert.True(rpcList2.IsReferred);
        Assert.Equal(333, rpcProp.Val["n1"][123][0]);
    }

    /// <summary>
    /// Unit test for costume Rpc properties.
    /// </summary>
    [Fact]
    public void TestCostumeRpcProp()
    {
        var costumeRpcContainerProp = new CostumeRpcContainerProperty1();
        RpcComplexProperty<CostumeRpcContainerProperty1> rpcProp = new(costumeRpcContainerProp);
        rpcProp.Init("test_costume_rpc_prop", RpcPropertySetting.FastSync);

        rpcProp.Val.SubListProperty.Add("111");

        CostumeRpcContainerProperty1 cprop = rpcProp;
        cprop.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 1.0f;

        Assert.True(costumeRpcContainerProp.IsReferred);
        Assert.True(costumeRpcContainerProp.SubCostumerContainerRpcContainerProperty.IsReferred);
        Assert.Equal("111", rpcProp.Val.SubListProperty[0]);
        Assert.Equal(1.0f, rpcProp.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty);
    }

    /// <summary>
    /// Unit test for property serialization.
    /// </summary>
    [Fact]
    public void TestPropertySerialization()
    {
        var entity = new TestEntity();
        var shadowEntity = new TestShadowEntity();
        RpcHelper.BuildPropertyTree(entity, RpcServerHelper.AllowedRpcPropertyGenTypes);
        RpcHelper.BuildPropertyTree(shadowEntity, RpcClientHelper.AllowedRpcPropertyGenTyeps);

        entity.TestRpcProp.Val.Add("1");
        entity.TestRpcProp.Val.Add("2");
        entity.TestRpcProp.Val.Add("3");

        entity.TestRpcPlaintPropStr.Val = "hello, LPS";

        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("a");
        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("b");
        entity.TestCostumeRpcContainerProperty1.Val.SubListProperty.Add("c");

        entity.TestCostumeRpcContainerProperty1.Val.SubCostumerContainerRpcContainerProperty.SubFloatProperty = 100.0f;

        entity.TestCostumeRpcContainerProperty2.Val.SubFloatProperty = 200.0f;

        entity.FullSync((_, content) => { shadowEntity.FromSyncContent(content); });

        Assert.Equal("1", shadowEntity.TestRpcProp.Val[0]);
        Assert.Equal("2", shadowEntity.TestRpcProp.Val[1]);
        Assert.Equal("3", shadowEntity.TestRpcProp.Val[2]);

        Assert.Equal("hello, LPS", shadowEntity.TestRpcPlaintPropStr);

        Assert.Equal("a", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[0]);
        Assert.Equal("b", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[1]);
        Assert.Equal("c", shadowEntity.TestCostumeRpcContainerProperty1.Val.SubListProperty[2]);

        var propValue = shadowEntity
            .TestCostumeRpcContainerProperty1
            .Val.SubCostumerContainerRpcContainerProperty
            .SubFloatProperty;
        Assert.Equal(100.0f, propValue);

        Assert.Equal(
            200.0f,
            shadowEntity.TestCostumeRpcContainerProperty2.Val.SubFloatProperty);

        Assert.True(CheckReferred(shadowEntity.TestRpcProp.Val));
        Assert.True(CheckReferred(shadowEntity.TestCostumeRpcContainerProperty1.Val));
        Assert.True(CheckReferred(shadowEntity.TestCostumeRpcContainerProperty2.Val));
    }

    /// <summary>
    /// Unit test for property change notification.
    /// </summary>
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
            Assert.Equal(new List<string> { "111", "222" }, val);
            Assert.Equal(new List<string> { "333", "333", "333" }, newVal);
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

    /// <summary>
    /// Unittest for dict property change notification.
    /// </summary>
    [Fact]
    public void TestDictPropertyChangeNotification()
    {
        var entity = new TestEntity();
        var removeCnt = 0;
        var updateCnt = 0;
        var clearCnt = 0;

        entity.TestComplexRpcProp.Val.OnRemoveElem = (key, _) =>
        {
            ++removeCnt;
            Assert.Equal("key_1", key);
        };

        entity.TestComplexRpcProp.Val.OnUpdatePair = (key, val, _) =>
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

    private static bool CheckReferred(RpcPropertyContainer? container)
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
}