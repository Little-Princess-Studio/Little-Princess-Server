using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Common.Core.Rpc.RpcProperty.RpcContainer;

namespace LPS.Client.Core.Rpc.RpcProperty;

public class RpcShadowComplexProperty<T> : RpcComplexPropertyBase<T>
    where T : RpcPropertyContainer
{
    public RpcShadowComplexProperty(string name) : base(name, RpcPropertySetting.Shadow, null!)
    {
    }
}