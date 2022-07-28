using LPS.Common.Core.Rpc.RpcPropertySync;

namespace LPS.Common.Core.Rpc.RpcProperty;

public interface IRpcPropertyOnNotifyResolver
{
    void OnNotify(RpcPropertySyncOperation operation, List<string> path, RpcPropertyContainer? @new);
}