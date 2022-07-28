using LPS.Common.Core.Rpc.RpcProperty;

namespace LPS.Server.Core.Rpc.RpcProperty;

public class RpcPlaintProperty<T> : RpcPlaintPropertyBase<T>
{
    public RpcPlaintProperty(string name, RpcPropertySetting setting, T value) : base(name, setting, value)
    {
    }
}