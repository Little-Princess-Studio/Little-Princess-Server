using System.Collections;
using System.Linq;
using System.Reflection;
using LPS.Core.Ipc;
using LPS.Core.Ipc.SyncMessage;
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

        AddPlainMessage(timeCircle);
        AddListMessage(timeCircle);
        AddDictMessage(timeCircle);

        var slots = GetSlot(timeCircle);

        timeCircle.Tick(50);
        timeCircle.Tick(50);
        timeCircle.Tick(50);

        Assert.True(true);
    }

    private static void AddPlainMessage(TimeCircle timeCircle)
    {
        var mailbox1 = new MailBox("test_id", "127.0.0.1", 88, 9999);
        var plainMsg1 = new RpcPlainPropertySyncMessage(mailbox1, RpcPropertySyncOperation.SetValue, "testpath",
            RpcSyncPropertyType.Plaint);
        plainMsg1.Val = new RpcPropertyContainer<string>("1111");

        var plainMsg2 = new RpcPlainPropertySyncMessage(mailbox1, RpcPropertySyncOperation.SetValue, "testpath",
            RpcSyncPropertyType.Plaint);
        plainMsg2.Val = new RpcPropertyContainer<string>("2222");

        timeCircle.AddPropertySyncMessage(
            plainMsg1,
            0,
            true);

        timeCircle.AddPropertySyncMessage(
            plainMsg2,
            0,
            true);

        var slot = GetSlot(timeCircle)[0];
        var queue = slot.FindOrderedSyncQueue(mailbox1);
        Assert.NotNull(queue);
        
        var arr = queue!.ToArray();
        Assert.Single(arr);

        var msg = (RpcPlainPropertySyncMessage)arr[0];
        Assert.NotNull(msg);
        
        Assert.Equal(RpcPropertySyncOperation.SetValue, msg.Operation);
        
        var val = (RpcPropertyContainer<string>) msg.Val;
        Assert.NotNull(val);
        Assert.Equal("2222", val.Value);
    }
    
    private static void AddListMessage(TimeCircle timeCircle)
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
                true);

            for (int j = i; j < i + 5; ++j)
            {
                var listMsg2 = new RpcListPropertySyncMessage(mailbox1,
                    RpcPropertySyncOperation.RemoveElem, "testpath2", RpcSyncPropertyType.List);
                listMsg2.Action!(j);
                timeCircle.AddPropertySyncMessage(
                    listMsg2,
                    70,
                    true);
            }
        }
        var slot = GetSlot(timeCircle)[0];
        var queue = slot.FindOrderedSyncQueue(mailbox1);
        Assert.NotNull(queue);
        
        var arr = queue!.ToArray();
        Assert.Equal(10, arr.Length);
        
        var msg = (RpcListPropertySyncMessage)arr[0];
        Assert.NotNull(msg);
        
        Assert.Equal(RpcPropertySyncOperation.AddListElem, msg.Operation);
        var impl = msg.GetImpl<RpcListPropertyAddSyncMessageImpl>();
        Assert.NotNull(impl);

        var addInfo = impl.GetAddInfo()
            .Select(container => ((RpcPropertyContainer<int>)container).Value)
            .ToArray();
        Assert.Equal(addInfo, new []{1, 2, 3, 4, 5});
    }
    
    private static void AddDictMessage(TimeCircle timeCircle)
    {
        var mailbox3 = new MailBox("test_id1", "127.0.0.1", 88, 9999);
        for (int i = 0; i < 5; ++i)
        {
            var dictMsg = new RpcDictPropertySyncMessage(mailbox3, 
                RpcPropertySyncOperation.UpdateDict, "testpath3", RpcSyncPropertyType.Dict);
            dictMsg.Action!(i, new RpcPropertyContainer<string>($"{i}"));
            timeCircle.AddPropertySyncMessage(dictMsg,
                120,
                true);

            for (int j = i; j < i + 10; ++j)
            {
                var dictMsg2 = new RpcDictPropertySyncMessage(mailbox3, 
                    RpcPropertySyncOperation.RemoveElem, "testpath3", RpcSyncPropertyType.Dict);
                dictMsg2.Action!(j);
                timeCircle.AddPropertySyncMessage(dictMsg2,
                    120,
                    true);
            }
        }
    }

    [Fact]
    public void TestCircleNotKeepOrder()
    {
        
    }
}