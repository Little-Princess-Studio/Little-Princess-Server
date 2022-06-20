using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;

namespace LPS.Client.Entity
{
    [EntityClass]
    public class Untrusted : ShadowClientEntity
    {
        public readonly RpcComplexProperty<RpcList<string>> TestRpcProp =
            new(nameof(Untrusted.TestRpcProp), RpcPropertySetting.Permanent, new RpcList<string>());

        public readonly RpcPlainProperty<string> TestRpcPlainPropStr =
            new(nameof(Untrusted.TestRpcPlainPropStr), RpcPropertySetting.Permanent, "");
    }
}