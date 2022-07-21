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
    [Flags]
    public enum RpcPropertySetting
    {
        None = 0x00000000, // None prop acts same as normal prop
        Permanent = 0x00000001, // Permanent prop will be saved into DB

        ServerOnly =
            0x00000010, // ServerOnly prop will not sync with client-side, but will be serialized/deserialized when entity transferred  
        ServerToShadow = 0x00000100, // ServerClient is same as ServerOnly prop, but also for sync to shadow entities
        FastSync = 0x00001000, // FastSync will sync as fast as possiable to shadow, ignoring prop change order
        KeepSendOrder = 0x00010000, // KeepSendOrder will sync props with keeping prop change order
        Shadow = 0x00100000, // Shadow is for shadow entities
    }

    public abstract class RpcProperty
    {
        public readonly string Name;
        public readonly RpcPropertySetting Setting;
        public BaseEntity? Owner;
        protected RpcPropertyContainer Value;

        public bool IsShadowProperty => this.Setting.HasFlag(RpcPropertySetting.Shadow);
        public bool ShouldSyncToClient => this.Setting.HasFlag(RpcPropertySetting.ServerToShadow);

        protected RpcProperty(string name, RpcPropertySetting setting, RpcPropertyContainer value)
        {
            Name = name;
            Setting = setting;
            Value = value;
        }

        public abstract Any ToProtobuf();
        public abstract void FromProtobuf(Any content);

        public void OnNotify(RpcPropertySyncOperation operation, List<string> path, object? old, object? @new)
        {
            Console.WriteLine($"[OnNotify] {operation}, {string.Join(".", path)}, {old} -> {@new}");
            switch (operation)
            {
                case RpcPropertySyncOperation.SetValue:
                    break;
                case RpcPropertySyncOperation.UpdateDict:
                    break;
                case RpcPropertySyncOperation.AddListElem:
                    break;
                case RpcPropertySyncOperation.RemoveElem:
                    break;
                case RpcPropertySyncOperation.Clear:
                    break;
                case RpcPropertySyncOperation.InsertElem:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
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

    public class RpcShadowPlaintProperty<T> : RpcPlainProperty<T>
    {
        public RpcShadowPlaintProperty(string name) : base(name, RpcPropertySetting.Shadow, default(T)!)
        {
        }
    }

    public class RpcShadowComplexProperty<T> : RpcComplexProperty<T>
        where T : RpcPropertyContainer
    {
        public RpcShadowComplexProperty(string name) : base(name, RpcPropertySetting.Shadow, null!)
        {
        }
    }

    public class RpcComplexProperty<T> : RpcProperty
        where T : RpcPropertyContainer
    {
        public RpcComplexProperty(string name, RpcPropertySetting setting, T value)
            : base(name, setting, value)
        {
            if (!this.IsShadowProperty)
            {
                this.Value.Name = name;
                this.Value.IsReferred = true;

                this.Value.UpdateTopOwner(this);
            }
        }

        private void Set(T value)
        {
            if (this.IsShadowProperty)
            {
                throw new Exception("Shadow property cannot be modified manually");
            }
            
            var container = ((RpcPropertyContainer<T>) this.Value);
            var old = container.Value;

            old.RemoveFromPropTree();
            container.InsertToPropTree(null, this.Name, this);
        }

        private T Get()
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

        public override void FromProtobuf(Any content)
        {
            if (!this.IsShadowProperty)
            {
                this.Val.RemoveFromPropTree();
            }
            this.Value = (T) RpcHelper.CreateRpcPropertyContainerByType(typeof(T), content);
            this.Val.InsertToPropTree(null, this.Name, this);
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
            this.Value.Name = name;
        }

        private void Set(T value)
        {
            if (this.IsShadowProperty)
            {
                throw new Exception("Shadow property cannot be modified manually");
            }

            ((RpcPropertyContainer<T>) this.Value).Value = value;
        }

        private T Get()
        {
            return (RpcPropertyContainer<T>) this.Value;
        }

        public T Val
        {
            get => Get();
            set => Set(value);
        }

        public static implicit operator T(RpcPlainProperty<T> container) => container.Val;

        public override Any ToProtobuf()
        {
            return Any.Pack(RpcHelper.RpcArgToProtobuf(this.Val));
        }

        public override void FromProtobuf(Any content)
        {
            this.Value =
                (RpcPropertyContainer<T>) RpcHelper.CreateRpcPropertyContainerByType(
                    typeof(RpcPropertyContainer<T>),
                    content);
            this.Value.Name = this.Name;
            this.Value.IsReferred = true;
        }
    }
}