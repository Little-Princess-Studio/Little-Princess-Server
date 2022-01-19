using LPS.Core.Entity;

/*
 * There are 3 way to implement rpc property:
 * 
 * 1. Use After-compile technique such as Mono.Ceil. This approch allow us define proerpty such as follow:
 *
 * [RpcMethod(auth, setting)]
 * public string RpcProp() { get; set } 
 *
 * This way needs us do post-compile process on output of LPS.Server.Dll to inject extra IL code on properties'
 * getter and setter method (such as null-check && notification and so on)
 * Post-compile process is like a magic, which is actually a 2-step compile.
 *
 * 2. Manually write duplicate setter code as follow:
 *
 *  [RpcProp(auth, setting)]
 *  public string rpcProp_ = ""
 *  public string RpcProp() { get => rpcProp_; set { do something here... } }
 *
 * which is like the code template like dependency property of WPF, which is an ugly but easy way.
 *
 * 3. Use RpcProperty<T> as the property type as follow:
 *
 * public readonly RpcProperty<string> RpcProp = new ("RpcProp", auth, setting);
 *
 * This way is a trade-off between 1 and 2 above, but this way is hardly to handle complex property type's change
 * notification such as:
 *
 * RpcDictionary<string, RpcDictionary<int, RpcList<int>>> RpcComplexProp { get; set; }
 *
 * How would the
 *
 * RpcComplexProp["a"][1][3] = 10
 *
 * to do the notification of sync?
 *
 * And how can we define costume rpc prop type?
 */

namespace LPS.Core.Rpc.RpcProperty
{
    public enum RpcPropertySetting
    {
        None = 0x00000000,
        Permanent = 0x00000001,
        ServerOwn = 0x00000010,
        ClientOwn = 0x00000100,
        FastSync = 0x00001000,
    }

    public abstract class RpcProperty
    {
        public readonly string Name;
        public BaseEntity? Owner;

        protected RpcProperty(string name)
        {
            Name = name;
        }
    }
    
    public class RpcProperty<T> : RpcProperty
    {
        public readonly RpcPropertySetting Setting;
        protected T value_;

        public RpcProperty(string name, RpcPropertySetting setting, T value): base(name)
        {
            this.Setting = setting;
            this.value_ = value;
        }

        public void Set(T value)
        {
            this.value_ = value;
        }
    }

    public class ShadowRpcProperty<T> : RpcProperty
    {
        protected T value_;
        
        public Action<T, T>? OnChanged { get; set; }

        public ShadowRpcProperty(string name) : base(name)
        {
        }
    }
}
