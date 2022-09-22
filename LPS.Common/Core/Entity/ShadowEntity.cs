using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Server.Core.Rpc;
using LPS.Server.Core.Rpc.InnerMessages;

namespace LPS.Common.Core.Entity
{
    [EntityClass]
    public class ShadowEntity : BaseEntity
    {
        [RpcMethod(Authority.ClientStub)]
        public void OnSync()
        {
        }
        
        public void FromSyncContent(Any syncBody)
        {
            if (syncBody.Is(DictWithStringKeyArg.Descriptor))
            {
                var content = syncBody.Unpack<DictWithStringKeyArg>();

                foreach (var (key, value) in content.PayLoad)
                {
                    if (this.PropertyTree!.ContainsKey(key))
                    {
                        var prop = this.PropertyTree[key];
                        prop.FromProtobuf(value);
                    }
                    else
                    {
                        Debug.Logger.Warn($"Missing sync property {key} in {this.GetType()}");
                    }
                }
            }
        }

    }    
}
