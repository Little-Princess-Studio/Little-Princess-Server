using LPS.Common.Core.Rpc.RpcProperty;

namespace LPS.Client.Core.Rpc.RpcProperty;

public class RpcShadowPlaintProperty<T> : RpcPlaintPropertyBase<T>
{
    public RpcShadowPlaintProperty(string name) : base(name, RpcPropertySetting.Shadow, default(T)!)
    {
    }
}