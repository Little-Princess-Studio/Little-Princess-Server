using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.RpcProperty;

namespace LPS.Logic.Entity
{   
    [EntityClass]
    public class Untrusted : ServerClientEntity
    {
        public readonly RpcComplexProperty<RpcList<string>> RpcProp = 
            new (nameof(Untrusted.RpcProp), RpcPropertySetting.Permanent, new RpcList<string>());

        public readonly RpcPlainProperty<string> RpcPlainPropStr = 
            new (nameof(Untrusted.RpcPlainPropStr), RpcPropertySetting.Permanent, "");
        
        public Untrusted(string desc) : base(desc)
        {
            Logger.Debug($"Untrusted created, desc : {desc}");
        }

        public Untrusted()
        {
        }

        [RpcMethod(Authority.ClientOnly)]
        public ValueTask<string> Echo(string msg)
        {
            return ValueTask.FromResult("echo:" + msg);
        }
    }
}
