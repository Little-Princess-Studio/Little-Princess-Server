using LPS.Client.Core.Entity;
using LPS.Client.Core.Rpc.RpcProperty;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Common.Core.Rpc.RpcProperty.RpcContainer;

namespace LPS.Client.Entity
{
    [EntityClass]
    public class Untrusted : ShadowClientEntity
    {
        public readonly RpcShadowComplexProperty<RpcList<string>> TestRpcProp =
            new(nameof(Untrusted.TestRpcProp));

        public readonly RpcShadowPlaintProperty<string> TestRpcPlaintPropStr =
            new(nameof(Untrusted.TestRpcPlaintPropStr));
    }
}