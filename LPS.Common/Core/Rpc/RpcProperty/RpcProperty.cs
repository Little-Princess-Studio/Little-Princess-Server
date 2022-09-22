using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Entity;
using LPS.Common.Core.Rpc.RpcPropertySync;

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

namespace LPS.Common.Core.Rpc.RpcProperty
{
    public delegate void OnSetValueCallBack<in T>(T oldVal, T newVal);

    public delegate void OnUpdateValueCallBack<in TK, in TV>(TK key, TV oldVal, TV newVal);

    public delegate void OnAddListElemCallBack<in T>(T newVal);

    public delegate void OnRemoveElemCallBack<in TK, in TV>(TK key, TV oldVal);

    public delegate void OnClearCallBack();

    public delegate void OnInsertItemCallBack<in T>(int index, T newVal);

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

    public interface ISendPropertySyncMessage
    {
        void SendSyncMsg(bool keepOrder, uint delayTime, RpcPropertySyncMessage syncMsg);
    }

    public abstract class RpcProperty
    {
        public readonly string Name;
        public readonly RpcPropertySetting Setting;
        public BaseEntity? Owner;
        protected RpcPropertyContainer Value;
        public IRpcPropertyOnNotifyResolver? NotifyResolver { get; set; }
        public bool NeedResolve => this.NotifyResolver != null;

        public bool IsShadowProperty => this.Setting.HasFlag(RpcPropertySetting.Shadow);
        public bool ShouldSyncToShadow => this.Setting.HasFlag(RpcPropertySetting.ServerToShadow);
        public ISendPropertySyncMessage? SendSyncMsgImpl { get; set; }

        protected RpcProperty(string name, RpcPropertySetting setting, RpcPropertyContainer value)
        {
            Name = name;
            Setting = setting;
            Value = value;
        }

        public abstract Any ToProtobuf();
        public abstract void FromProtobuf(Any content);

        public void OnNotify(RpcPropertySyncOperation operation, List<string> path, RpcPropertyContainer? @new,
            RpcSyncPropertyType propertyType)
        {
            if (this.SendSyncMsgImpl == null || IsShadowProperty || !ShouldSyncToShadow)
            {
                return;
            }

            Console.WriteLine($"[OnNotify] {operation}, {string.Join(".", path)}, {@new}");
            switch (operation)
            {
                case RpcPropertySyncOperation.SetValue:
                    this.OnSetValueInternal(path, @new!);
                    break;
                case RpcPropertySyncOperation.UpdateDict:
                    this.OnUpdateDictInternal(path, @new!);
                    break;
                case RpcPropertySyncOperation.AddListElem:
                    this.OnAddListElemInternal(path, @new!);
                    break;
                case RpcPropertySyncOperation.RemoveElem:
                    this.OnRemoveElemInternal(path, propertyType);
                    break;
                case RpcPropertySyncOperation.Clear:
                    this.OnClearInternal(path, propertyType);
                    break;
                case RpcPropertySyncOperation.InsertElem:
                    this.OnInsertItem(path, @new!);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private void OnSetValueInternal(List<string> path, RpcPropertyContainer newVal)
        {
            var fullPath = String.Join(',', path);
            var syncMsg = new RpcPlaintAndCostumePropertySyncMessage(this.Owner!.MailBox,
                RpcPropertySyncOperation.SetValue,
                fullPath, RpcSyncPropertyType.PlaintAndCostume, newVal);
            this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
        }

        private void OnUpdateDictInternal(List<string> path, RpcPropertyContainer newVal)
        {
            var key = path.Last();
            path.RemoveAt(path.Count - 1);
            var fullPath = String.Join('.', path);
            var syncMsg = new RpcDictPropertySyncMessage(this.Owner!.MailBox, RpcPropertySyncOperation.UpdateDict,
                fullPath, RpcSyncPropertyType.Dict);
            syncMsg.Action!(key, newVal);
            this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
        }

        private void OnAddListElemInternal(List<string> path, RpcPropertyContainer newVal)
        {
            var idx = Convert.ToInt32(path.Last());
            path.RemoveAt(path.Count - 1);
            var fullPath = String.Join('.', path);
            var syncMsg = new RpcListPropertySyncMessage(this.Owner!.MailBox, RpcPropertySyncOperation.AddListElem,
                fullPath, RpcSyncPropertyType.List);
            syncMsg.Action!(idx, newVal);
            this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
        }

        private void OnRemoveElemInternal(List<string> path, RpcSyncPropertyType propertyType)
        {
            var key = path.Last();
            path.RemoveAt(path.Count - 1);
            var fullPath = String.Join('.', path);

            RpcPropertySyncMessage syncMsg;
            
            switch (propertyType)
            {
                case RpcSyncPropertyType.List:
                    var syncMsgList = new RpcListPropertySyncMessage(this.Owner!.MailBox,
                        RpcPropertySyncOperation.Clear, fullPath, propertyType);
                    syncMsgList.Action!(Convert.ToInt32(key));
                    syncMsg = syncMsgList;
                    break;
                case RpcSyncPropertyType.Dict:
                    var syncMsgDict = new RpcListPropertySyncMessage(this.Owner!.MailBox,
                        RpcPropertySyncOperation.Clear, fullPath, propertyType);
                    syncMsgDict.Action!(Convert.ToInt32(key));
                    syncMsg = syncMsgDict;
                    break;
                case RpcSyncPropertyType.PlaintAndCostume:
                default:
                    throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, null);
            }
            
            this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg);
        }

        private void OnClearInternal(List<string> path, RpcSyncPropertyType propertyType)
        {
            var fullPath = String.Join('.', path);

            RpcPropertySyncMessage syncMsg = propertyType switch
            {
                RpcSyncPropertyType.List => new RpcListPropertySyncMessage(this.Owner!.MailBox,
                    RpcPropertySyncOperation.Clear, fullPath, propertyType),
                RpcSyncPropertyType.Dict => new RpcDictPropertySyncMessage(this.Owner!.MailBox,
                    RpcPropertySyncOperation.Clear, fullPath, propertyType),
                _ => throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, null)
            };
            this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg);
        }

        private void OnInsertItem(List<string> path, RpcPropertyContainer newVal)
        {
            var idx = Convert.ToInt32(path.Last());
            path.RemoveAt(path.Count - 1);
            var fullPath = String.Join('.', path);
            var syncMsg = new RpcListPropertySyncMessage(this.Owner!.MailBox, RpcPropertySyncOperation.InsertElem,
                fullPath, RpcSyncPropertyType.List);
            syncMsg.Action!(idx, newVal);
            this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
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

        public void Set(T value)
        {
            if (this.IsShadowProperty)
            {
                throw new Exception("Shadow property cannot be modified manually");
            }

            var container = this.Value;
            var old = this.Value;

            old.RemoveFromPropTree();
            old.InsertToPropTree(null, this.Name, this);

            this.Value = value;
            var path = new List<string> {this.Name};
            this.OnNotify(RpcPropertySyncOperation.SetValue, path, value, RpcSyncPropertyType.PlaintAndCostume);
        }

        private T Get()
        {
            return (T) this.Value;
        }

        public T Val
        {
            get => Get();
            private set => Set(value);
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
            else
            {
                // for shadow property, generic rpc container may not be registered
                if (!RpcHelper.IsRpcContainerRegistered(typeof(T)))
                {
                    RpcHelper.RegisterRpcPropertyContainer(typeof(T));
                }
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

            var old = ((RpcPropertyContainer<T>) this.Value).Value;
            ((RpcPropertyContainer<T>) this.Value).Value = value;

            var path = new List<string> {this.Name};
            this.OnNotify(RpcPropertySyncOperation.SetValue, path, this.Value, RpcSyncPropertyType.PlaintAndCostume);
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