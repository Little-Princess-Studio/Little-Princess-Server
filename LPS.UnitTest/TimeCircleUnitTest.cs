using LPS.Core.Ipc;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;
using LPS.Core.Rpc.RpcPropertySync;
using Xunit;

namespace LPS.UnitTest;

public class TimeCircleUnitTest
{
    [Fact]
    public void TestCircleKeepOrder()
    {
        var timeCircle = new TimerCircle(50, 1000);

        AddPliantMessage(timeCircle);
        AddListMessage(timeCircle);
        AddDictMessage(timeCircle);

        timeCircle.Tick(50);
        timeCircle.Tick(50);
        timeCircle.Tick(50);

        Assert.True(true);
    }

    private static void AddDictMessage(TimerCircle timeCircle)
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

    private static void AddListMessage(TimerCircle timeCircle)
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
    }

    private static void AddPliantMessage(TimerCircle timeCircle)
    {
        var mailbox1 = new MailBox("test_id", "127.0.0.1", 88, 9999);
        var plaintMsg1 = new RpcPlaintPropertySyncMessage(mailbox1, RpcPropertySyncOperation.SetValue, "testpath",
            RpcSyncPropertyType.Plaint);
        plaintMsg1.Val = new RpcPropertyContainer<string>("1111");

        var plaintMsg2 = new RpcPlaintPropertySyncMessage(mailbox1, RpcPropertySyncOperation.SetValue, "testpath",
            RpcSyncPropertyType.Plaint);
        plaintMsg2.Val = new RpcPropertyContainer<string>("2222");

        timeCircle.AddPropertySyncMessage(
            plaintMsg1,
            0,
            true);

        timeCircle.AddPropertySyncMessage(
            plaintMsg2,
            0,
            true);
    }

    [Fact]
    public void TestCircleNotKeepOrder()
    {
        
    }
}