using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

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
        ServerOnly = 0x00000010,
        ServerClient = 0x00000100,
        FastSync = 0x00001000,
        KeepSendOrder = 0x00010000,
        Shadow = 0x00100000,
    }

    public abstract class RpcProperty
    {
        public readonly string Name;
        public readonly RpcPropertySetting Setting;
        public BaseEntity? Owner;
        protected readonly RpcPropertyContainer Value;

        public bool IsShadowProperty => ((uint)this.Setting & (uint)RpcPropertySetting.Shadow) == 1;
        
        protected RpcProperty(string name, RpcPropertySetting setting, RpcPropertyContainer value)
        {
            Name = name;
            Setting = setting;
            Value = value;
        }

        public abstract IMessage ToProtobuf();

        public void OnNotify(RpcPropertySyncOperation operation, List<string> path, object? old, object? @new)
        {
            Console.WriteLine($"[OnNotify] {operation}, {string.Join(".", path)}, {old} -> {@new}");
        }

        public void OnChange(List<string> path, object? oldVal, object? newVal)
        {
            // var syncRpc = new PropertySync();
            //
            // foreach (var p in path)
            // {
            //     syncRpc.Path.Add(p);
            // }
            //
            // syncRpc.SyncType = (uint)RpcPropertySyncOperation.SetValue;
            // syncRpc.SyncArg = (Any)RpcHelper.RpcArgToProtobuf(newVal);
        }
    }

    public class RpcComplexProperty<T> : RpcProperty
        where T : RpcPropertyContainer
    {
        public RpcComplexProperty(string name, RpcPropertySetting setting, T value)
            : base(name, setting, value)
        {
            this.Value.Owner = this;
            this.Value.Name = name;
            this.Value.IsReferred = true;
            
            this.Value.UpdateTopOwner(this);
        }

        public void Set(T value)
        {
            var container = ((RpcPropertyContainer<T>) this.Value);

            var old = container.Value;
            old.Parent = null;
            old.IsReferred = false;
            old.UpdateTopOwner(null);

            container.Value = value;
            container.Value.Parent = null;
            container.Value.IsReferred = true;
            container.Value.UpdateTopOwner(this);
        }

        public T Get()
        {
            return (T) this.Value;
        }

        public T Val
        {
            get => Get();
            set => Set(value);
        }

        public static implicit operator T(RpcComplexProperty<T> complex) => complex.Val;

        public override Any ToProtobuf()
        {
            return this.Val.ToRpcArg();
        }
    }

    public class RpcPlainProperty<T> : RpcProperty
    {
        static RpcPlainProperty()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
        }

        private RpcPlainProperty() : base("", RpcPropertySetting.None, null)
        {
        }

        public RpcPlainProperty(string name, RpcPropertySetting setting, T value)
            : base(name, setting, new RpcPropertyContainer<T>(value))
        {
            this.Value.Owner = this;
            this.Value.Name = name;
        }

        public void Set(T value)
        {
            ((RpcPropertyContainer<T>) this.Value).Value = value;
        }

        public T Get()
        {
            return (RpcPropertyContainer<T>) this.Value;
        }

        public T Val
        {
            get => Get();
            set => Set(value);
        }

        public static implicit operator T(RpcPlainProperty<T> container) => container.Val;

        public override IMessage ToProtobuf()
        {
            return RpcHelper.RpcArgToProtobuf(this.Val);
        }
    }
}