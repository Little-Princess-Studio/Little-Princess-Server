using Google.Protobuf.WellKnownTypes;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Entity
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
