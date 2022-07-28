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

    public abstract class RpcProperty
    {
        public readonly string Name;
        public readonly RpcPropertySetting Setting;
        public BaseEntity? Owner;
        protected RpcPropertyContainer Value;
        public IRpcPropertyOnNotifyResolver? NotifyResolver { get; set; }
        public bool NeedResolve => this.NotifyResolver != null;

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
        
        public void OnNotify(RpcPropertySyncOperation operation, List<string> path, RpcPropertyContainer? @new)
        {
            this.NotifyResolver?.OnNotify(operation, path, @new);
            
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
                    this.OnRemoveElemInternal(path);
                    break;
                case RpcPropertySyncOperation.Clear:
                    this.OnClearInternal(path);
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
            // var entity = this.Owner;
            //
            // if (entity == null)
            // {
            //     return;
            // }
            //
            // var newType = newVal.GetType();
            // var pathStr = string.Join(",", path);
            // if (newType.IsGenericType)
            // {
            //     if (newType.GetGenericTypeDefinition() == typeof(RpcList<>))
            //     {
            //         var msg = new RpcListPropertySyncMessage(entity.MailBox, RpcPropertySyncOperation.SetValue,
            //             pathStr);
            //         return;
            //     }
            //     else if (newType.GetGenericTypeDefinition() == typeof(RpcDictionary<,>))
            //     {
            //         var msg = new RpcDictPropertySyncMessage(entity.MailBox, RpcPropertySyncOperation.SetValue,
            //             pathStr);
            //         return;
            //     }
            //
            //     throw new Exception($"Invalid value type {newVal.GetType()}");
            // }
            //
            // var msg = new RpcPlaintAndCostumePropertySyncMessage(entity.MailBox, RpcPropertySyncOperation.SetValue,
            //     pathStr, newVal);
        }

        private void OnUpdateDictInternal(List<string> path, RpcPropertyContainer newVal)
        {
            
        }

        private void OnAddListElemInternal(List<string> path, RpcPropertyContainer newVal)
        {
            
        }

        private void OnRemoveElemInternal(List<string> path)
        {
            
        }

        private void OnClearInternal(List<string> path)
        {
            
        }

        private void OnInsertItem(List<string> path, RpcPropertyContainer newVal)
        {
            
        }
    }
}