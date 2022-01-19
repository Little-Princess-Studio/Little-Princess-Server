using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    [EntityClass]
    public class ShadowEntity : BaseEntity
    {
        [RpcMethod(Authority.ClientStub)]
        public void OnSync()
        {
        }
    }    
}
