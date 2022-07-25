using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;

namespace LPS.Client.Entity
{
    [EntityClass]
    public class Untrusted : ShadowClientEntity
    {
        public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp =
            new(nameof(Untrusted.TestRpcProp));

        public readonly RpcShadowPlaintProperty<string> TestRpcPlainPropStr =
            new(nameof(Untrusted.TestRpcPlainPropStr));
    }
}