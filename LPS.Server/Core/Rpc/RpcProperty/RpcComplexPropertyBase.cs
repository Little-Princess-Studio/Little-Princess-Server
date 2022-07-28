using LPS.Common.Core.Rpc.RpcProperty;

namespace LPS.Server.Core.Rpc.RpcProperty;

public class RpcComplexProperty<T> : RpcComplexPropertyBase<T> where T: RpcPropertyContainer
{
    public RpcComplexProperty(string name, RpcPropertySetting setting, T value) : base(name, setting, value)
    {
    }
}