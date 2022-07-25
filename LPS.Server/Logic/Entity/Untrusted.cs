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
        public readonly RpcComplexProperty<RpcList<string>> TestRpcProp = 
            new (nameof(Untrusted.TestRpcProp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new RpcList<string>(3, "111"));

        public readonly RpcPlainProperty<string> TestRpcPlainPropStr = 
            new (nameof(Untrusted.TestRpcPlainPropStr), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, "Hello, LPS");
        
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
