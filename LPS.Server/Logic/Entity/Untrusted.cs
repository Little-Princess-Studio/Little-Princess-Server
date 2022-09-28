using System.Threading.Tasks;
using LPS.Common.Core.Debug;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Server.Core.Entity;
using LPS.Server.Core.Rpc.RpcProperty;

namespace LPS.Server.Logic.Entity
{   
    [EntityClass]
    public class Untrusted : ServerClientEntity
    {
        public readonly RpcComplexProperty<RpcList<string>> TestRpcProp = 
            new (nameof(Untrusted.TestRpcProp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, new RpcList<string>(3, "111"));

        public readonly RpcPlaintProperty<string> TestRpcPlaintPropStr = 
            new (nameof(Untrusted.TestRpcPlaintPropStr), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow, "Hello, LPS");
        
        public Untrusted(string desc) : base(desc)
        {
            Logger.Debug($"Untrusted created, desc : {desc}");
        }

        public Untrusted()
        {
        }

        [RpcMethod(Authority.ClientOnly)]
        public ValueTask TestChange()
        {
            this.TestRpcProp.Val.Add("222");
            this.TestRpcPlaintPropStr.Val = "Little Princess";
            return ValueTask.CompletedTask;
        }

        [RpcMethod(Authority.ClientOnly)]
        public ValueTask ChangeProp(string prop)
        {
            this.TestRpcPlaintPropStr.Val = prop;
            Logger.Debug($"[ChangeProp] prop = {prop}");
            return ValueTask.CompletedTask;
        }

        [RpcMethod(Authority.ClientOnly)]
        public ValueTask<string> Echo(string msg)
        {
            return ValueTask.FromResult("echo:" + msg);
        }
    }
}
