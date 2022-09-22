using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using LPS.Core.Ipc;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;
using LPS.Core.Rpc.RpcPropertySync;
using Xunit;

namespace LPS.UnitTest;

public class TimeCircleUnitTest
{
    private static TimeCircleSlot[] GetSlot(TimeCircle timeCircle)
    {
        return (TimeCircleSlot[]) typeof(TimeCircle)
            .GetField("slots_", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(timeCircle)!;
    }
    
    [Fact]
    public void TestCircleKeepOrder()
    {
        var timeCircle = new TimeCircle(50, 1000);

        AddPlainMessage(timeCircle, true);
        AddListMessage(timeCircle, true);
        AddDictMessage(timeCircle, true);

        var slots = GetSlot(timeCircle);

        timeCircle.Tick(50);
        timeCircle.Tick(50);
        timeCircle.Tick(50);

        var slot = GetSlot(timeCircle)[0];
        Assert.Equal(0, slot.GetSyncQueueLength(new MailBox("test_id1", "127.0.0.1", 99, 9999)));
        slot = GetSlot(timeCircle)[1];
        Assert.Equal(0, slot.GetSyncQueueLength(new MailBox("test_id2", "127.0.0.1", 99, 9999)));
        slot = GetSlot(timeCircle)[2];
        Assert.Equal(0, slot.GetSyncQueueLength(new MailBox("test_id3", "127.0.0.1", 99, 9999)));
    }

    private static void AddPlainMessage(TimeCircle timeCircle, bool keepOrder)
    {
        var mailbox1 = new MailBox("test_id1", "127.0.0.1", 88, 9999);
        var plainMsg1 = new RpcPlaintAndCostumePropertySyncMessage(mailbox1, RpcPropertySyncOperation.SetValue, "testpath",
            RpcSyncPropertyType.PlaintAndCostume,  new RpcPropertyContainer<string>("1111"));

        var plainMsg2 = new RpcPlaintAndCostumePropertySyncMessage(mailbox1, RpcPropertySyncOperation.SetValue, "testpath",
            RpcSyncPropertyType.PlaintAndCostume, new RpcPropertyContainer<string>("2222"));

        timeCircle.AddPropertySyncMessage(
            plainMsg1,
            0,
            keepOrder);

        timeCircle.AddPropertySyncMessage(
            plainMsg2,
            0,
            keepOrder);

        var slot = GetSlot(timeCircle)[0];

        if (keepOrder)
        {
            var queue = slot.FindOrderedSyncQueue(mailbox1);
            Assert.NotNull(queue);
        
            var arr = queue!.ToArray();
            Assert.Single(arr);

            var msg = (RpcPlaintAndCostumePropertySyncMessage)arr[0];
            Assert.NotNull(msg);
        
            Assert.Equal(RpcPropertySyncOperation.SetValue, msg.Operation);
        
            var val = (RpcPropertyContainer<string>) msg.Val;
            Assert.NotNull(val);
            Assert.Equal("2222", val.Value);   
        }
        else
        {
            var syncInfo = slot.FindRpcPropertySyncInfo(mailbox1, "testpath")!;
            Assert.NotNull(syncInfo);
            var arr = syncInfo.PropPath2SyncMsgQueue.ToArray();

            Assert.Single(arr);
            
            var msg = (RpcPlaintAndCostumePropertySyncMessage)arr[0];
            Assert.NotNull(msg);
        
            Assert.Equal(RpcPropertySyncOperation.SetValue, msg.Operation);
        
            var val = (RpcPropertyContainer<string>) msg.Val;
            Assert.NotNull(val);
            Assert.Equal("2222", val.Value);   
        }
    }
    
    private static void AddListMessage(TimeCircle timeCircle, bool keepOrder)
    {
        var mailbox1 = new MailBox("test_id2", "127.0.0.1", 88, 9999);
        for (int i = 0; i < 5; ++i)
        {
            var listMsg = new RpcListPropertySyncMessage(mailbox1,
                RpcPropertySyncOperation.AddListElem, "testpath2", RpcSyncPropertyType.List);
            listMsg.Action!(new RpcPropertyContainer<int>(i));
            timeCircle.AddPropertySyncMessage(
                listMsg,
                70,
                keepOrder);

            for (int j = i; j < i + 5; ++j)
            {
                var listMsg2 = new RpcListPropertySyncMessage(mailbox1,
                    RpcPropertySyncOperation.RemoveElem, "testpath2", RpcSyncPropertyType.List);
                listMsg2.Action!(j);
                timeCircle.AddPropertySyncMessage(
                    listMsg2,
                    70,
                    keepOrder);
            }
        }
        
        var slot = GetSlot(timeCircle)[1];

        if (keepOrder)
        {
            var queue = slot.FindOrderedSyncQueue(mailbox1);
            Assert.NotNull(queue);
        
            var arr = queue!.ToArray();
            Assert.Equal(10, arr.Length);
        
            CheckAddListMsg(arr[0], new int[] {0});
            CheckRemoveListMsg(arr[1], new int[] { 0, 1, 2, 3, 4});
            CheckAddListMsg(arr[2], new int[] {1});
            CheckRemoveListMsg(arr[3], new int[] { 1, 2, 3, 4, 5});
            CheckAddListMsg(arr[4], new int[] {2});
            CheckRemoveListMsg(arr[5], new int[] { 2, 3, 4, 5, 6});
            CheckAddListMsg(arr[6], new int[] {3});
            CheckRemoveListMsg(arr[7], new int[] { 3, 4, 5, 6, 7});
            CheckAddListMsg(arr[8], new int[] {4});
            CheckRemoveListMsg(arr[9], new int[] { 4, 5, 6, 7, 8});   
        }
        else
        {
            var syncInfo = slot.FindRpcPropertySyncInfo(mailbox1, "testpath2")!;
            Assert.NotNull(syncInfo);
            
            var arr = syncInfo.PropPath2SyncMsgQueue.ToArray();
            Assert.Equal(10, arr.Length);
            
            CheckAddListMsg(arr[0], new int[] {0});
            CheckRemoveListMsg(arr[1], new int[] { 0, 1, 2, 3, 4});
            CheckAddListMsg(arr[2], new int[] {1});
            CheckRemoveListMsg(arr[3], new int[] { 1, 2, 3, 4, 5});
            CheckAddListMsg(arr[4], new int[] {2});
            CheckRemoveListMsg(arr[5], new int[] { 2, 3, 4, 5, 6});
            CheckAddListMsg(arr[6], new int[] {3});
            CheckRemoveListMsg(arr[7], new int[] { 3, 4, 5, 6, 7});
            CheckAddListMsg(arr[8], new int[] {4});
            CheckRemoveListMsg(arr[9], new int[] { 4, 5, 6, 7, 8});
        }
    }

    private static void CheckAddListMsg<TElemType>(RpcPropertySyncMessage rpcMsg, TElemType[] data) where TElemType : IComparable
    {
        var msg = (RpcListPropertySyncMessage)rpcMsg;
        Assert.NotNull(msg);
        
        Assert.Equal(RpcPropertySyncOperation.AddListElem, msg.Operation);
        var impl = msg.GetImpl<RpcListPropertyAddSyncMessageImpl>();
        Assert.NotNull(impl);
        var addInfo = impl.GetAddInfo()
            .Select(container => ((RpcPropertyContainer<TElemType>)container).Value)
            .ToArray();
        Assert.Equal(data, addInfo);
    }
    
    private static void CheckRemoveListMsg(RpcPropertySyncMessage rpcMsg, int[] data)
    {
        var msg = (RpcListPropertySyncMessage)rpcMsg;
        Assert.NotNull(msg);
        
        Assert.Equal(RpcPropertySyncOperation.RemoveElem, msg.Operation);
        var impl = msg.GetImpl<RpcListPropertyRemoveElemMessageImpl>();
        Assert.NotNull(impl);
        var addInfo = impl.GetRemoveElemInfo()
            .ToArray();
        Assert.Equal(data, addInfo);
    }

    private static void AddDictMessage(TimeCircle timeCircle, bool keepOrder)
    {
        var mailbox3 = new MailBox("test_id3", "127.0.0.1", 88, 9999);
        for (int i = 0; i < 5; ++i)
        {
            var dictMsg = new RpcDictPropertySyncMessage(mailbox3, 
                RpcPropertySyncOperation.UpdateDict, "testpath3", RpcSyncPropertyType.Dict);
            dictMsg.Action!(i, new RpcPropertyContainer<string>($"{i}"));
            timeCircle.AddPropertySyncMessage(dictMsg,
                120,
                keepOrder);

            for (int j = i; j < i + 10; ++j)
            {
                var dictMsg2 = new RpcDictPropertySyncMessage(mailbox3, 
                    RpcPropertySyncOperation.RemoveElem, "testpath3", RpcSyncPropertyType.Dict);
                dictMsg2.Action!(j);
                timeCircle.AddPropertySyncMessage(dictMsg2,
                    120,
                    keepOrder);
            }
        }
        
        var slot = GetSlot(timeCircle)[2];

        if (keepOrder)
        {
            var queue = slot.FindOrderedSyncQueue(mailbox3);
            Assert.NotNull(queue);
        
            var arr = queue!.ToArray();
            Assert.Equal(10, arr.Length);

            CheckUpdateDictMsg(arr[0], new Dictionary<object, string> {{0, "0"}});
            CheckRemoveDictMsg(arr[1], new HashSet<object> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9});
            CheckUpdateDictMsg(arr[2], new Dictionary<object, string> {{1, "1"}});
            CheckRemoveDictMsg(arr[3], new HashSet<object> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10});
            CheckUpdateDictMsg(arr[4], new Dictionary<object, string> {{2, "2"}});
            CheckRemoveDictMsg(arr[5], new HashSet<object> { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11});
            CheckUpdateDictMsg(arr[6], new Dictionary<object, string> {{3, "3"}});
            CheckRemoveDictMsg(arr[7], new HashSet<object> { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12});
            CheckUpdateDictMsg(arr[8], new Dictionary<object, string> {{4, "4"}});
            CheckRemoveDictMsg(arr[9], new HashSet<object> { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13});
        }
        else
        {
            var syncInfo = slot.FindRpcPropertySyncInfo(mailbox3, "testpath3")!;
            Assert.NotNull(syncInfo);
            
            var arr = syncInfo.PropPath2SyncMsgQueue.ToArray();
            Assert.Single(arr);            
            
            var msg = (RpcDictPropertySyncMessage)arr[0];
            Assert.NotNull(msg);
            
            CheckRemoveDictMsg(msg, new HashSet<object> {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13});
        }
    }

    private static void CheckUpdateDictMsg<TElemType>(RpcPropertySyncMessage rpcMsg, Dictionary<object, TElemType> checkDict) where TElemType : IComparable
    {
        var msg = (RpcDictPropertySyncMessage)rpcMsg;
        Assert.NotNull(msg);

        Assert.Equal(RpcPropertySyncOperation.UpdateDict, msg.Operation);
        var updateDictImpl = msg.GetImpl<RpcDictPropertyUpdateSyncMessageImpl>();
        Assert.NotNull(updateDictImpl);
        var updateDictInfo = updateDictImpl.GetUpdateDictInfo();
        Assert.NotNull(updateDictInfo);
        
        var updateValueDict = updateDictInfo!
            .Select(kv =>
                new KeyValuePair<object, TElemType>(kv.Key, ((RpcPropertyContainer<TElemType>) (kv.Value)).Value))
            .OrderBy(kv => kv.Key);
        
        Assert.Equal(
            updateValueDict,
            checkDict.OrderBy(kv => kv.Key));
    }

    private static void CheckRemoveDictMsg(RpcPropertySyncMessage rpcMsg, HashSet<object> keys)
    {
        var msg = (RpcDictPropertySyncMessage)rpcMsg;
        Assert.NotNull(msg);

        Assert.Equal(RpcPropertySyncOperation.RemoveElem, msg.Operation);
        var removeDictImpl = msg.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
        Assert.NotNull(removeDictImpl);
        var removeDictInfo = removeDictImpl.GetRemoveDictInfo();
        Assert.NotNull(removeDictInfo);

        Assert.Equal(removeDictInfo, keys);
    }

    [Fact]
    public void TestCircleNotKeepOrder()
    {
        var timeCircle = new TimeCircle(50, 1000);

        AddPlainMessage(timeCircle, false);
        AddListMessage(timeCircle, false);
        AddDictMessage(timeCircle, false);

        var slots = GetSlot(timeCircle);

        timeCircle.Tick(50);
        timeCircle.Tick(50);
        timeCircle.Tick(50);

        var slot = GetSlot(timeCircle)[0];
        Assert.Null(slot.FindRpcPropertySyncInfo(new MailBox("test_id", "127.0.0.1", 99, 9999), "testpath"));;
        slot = GetSlot(timeCircle)[1];
        Assert.Null(slot.FindRpcPropertySyncInfo(new MailBox("test_id2", "127.0.0.1", 99, 9999), "testpath2"));
        slot = GetSlot(timeCircle)[2];
        Assert.Null(slot.FindRpcPropertySyncInfo(new MailBox("test_id3", "127.0.0.1", 99, 9999), "testpath3"));
    }
}