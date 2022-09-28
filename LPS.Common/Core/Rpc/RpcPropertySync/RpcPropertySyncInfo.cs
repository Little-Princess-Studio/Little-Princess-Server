using LPS.Common.Core.Rpc.InnerMessages;

namespace LPS.Common.Core.Rpc.RpcPropertySync;

public enum RpcSyncPropertyType
{
    PlaintAndCostume = 0,
    Dict = 1,
    List = 2,
}

public abstract class RpcPropertySyncInfo
{
    private readonly LinkedList<RpcPropertySyncMessage> propPath2SyncMsgQueue_ = new();
    public LinkedList<RpcPropertySyncMessage> PropPath2SyncMsgQueue => propPath2SyncMsgQueue_;
    public abstract void AddNewSyncMessage(RpcPropertySyncMessage msg);

    public readonly RpcSyncPropertyType RpcSyncPropertyType;

    protected RpcPropertySyncInfo(RpcSyncPropertyType rpcSyncPropertyType)
    {
        this.RpcSyncPropertyType = rpcSyncPropertyType;
    }

    public void Reset()
    {
        propPath2SyncMsgQueue_.Clear();
    }

    public void Enque(RpcPropertySyncMessage msg)
    {
        propPath2SyncMsgQueue_.AddLast(msg);
    }

    public RpcPropertySyncMessage? GetLastMsg()
        => propPath2SyncMsgQueue_.Count > 0 ? propPath2SyncMsgQueue_.Last() : null;

    public void PopLastMsg()
    {
        if (propPath2SyncMsgQueue_.Count > 0)
        {
            propPath2SyncMsgQueue_.RemoveLast();
        }
    }

    public void Clear() => propPath2SyncMsgQueue_.Clear();
}

public class RpcListPropertySyncInfo : RpcPropertySyncInfo
{
    public RpcListPropertySyncInfo() : base(RpcSyncPropertyType.List)
    {
    }

    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcListPropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}

public class RpcDictPropertySyncInfo : RpcPropertySyncInfo
{
    public RpcDictPropertySyncInfo() : base(RpcSyncPropertyType.Dict)
    {
    }

    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcDictPropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}

public class RpcPlaintAndCostumePropertySyncInfo : RpcPropertySyncInfo
{
    public RpcPlaintAndCostumePropertySyncInfo() : base(RpcSyncPropertyType.PlaintAndCostume)
    {
    }

    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcPlaintAndCostumePropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}