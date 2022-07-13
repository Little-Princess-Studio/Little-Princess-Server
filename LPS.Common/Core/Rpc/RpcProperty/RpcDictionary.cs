// using System.Collections.ObjectModel;
// using System.Linq.Expressions;
//

using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcDictionary<TK, TV> : RpcPropertyContainer
        where TK : notnull
    {
        private Dictionary<TK, RpcPropertyContainer<TV>> value_;

        public Dictionary<TK, RpcPropertyContainer<TV>> Value
        {
            get => value_;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, old: value_!, value);
                this.value_ = value;
            }
        }

        static RpcDictionary()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidKeyType<TK>();
        }

        public RpcDictionary() : base()
        {
            this.value_ = new();
            this.Children = new();
        }

        public override Any ToRpcArg()
        {
            IMessage? pbDictVal = null;
            DictWithStringKeyArg? pbChildren = null;

            if (this.Value.Count > 0)
            {
                pbDictVal = RpcHelper.RpcContainerDictToProtoBufAny(this);
            }

            if (this.Children!.Count > 0)
            {
                pbChildren = new DictWithStringKeyArg();

                foreach (var (name, value) in this.Children)
                {
                    pbChildren.PayLoad.Add(name, value.ToRpcArg());
                }
            }

            var pbRpc = new DictWithStringKeyArg();
            pbRpc.PayLoad.Add("value", pbDictVal == null ? Any.Pack(new NullArg()) : Any.Pack(pbDictVal));
            pbRpc.PayLoad.Add("children", pbChildren == null ? Any.Pack(new NullArg()) : Any.Pack(pbChildren));

            return Any.Pack(pbRpc);
        }

        public TV this[TK key]
        {
            get => this.Value[key].Value;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (this.Value.ContainsKey(key))
                {
                    this.HandleIfContainer(this.Value[key], value);

                    var old = this.Value[key].Value;
                    if (old is RpcPropertyContainer container)
                    {
                        container.IsReffered = false;
                    }

                    this.Value[key].SetWithoutNotify(value);
                    this.NotifyChange(RpcPropertySyncOperation.UpdateDict, this.Value[key].Name!, old!, value);
                }
                else
                {
                    var newContainer = new RpcPropertyContainer<TV>(value)
                    {
                        Parent = this,
                        Name = $"{key}",
                        IsReffered = true,
                    };

                    this.HandleIfContainer<TV>(newContainer, value);
                    this.Value[key] = newContainer;
                    this.NotifyChange(RpcPropertySyncOperation.UpdateDict, newContainer.Name, null, value);

                    this.Children!.Add($"{key}", newContainer);
                }
            }
        }

        public void Remove(TK key)
        {
            var elem = this.Value[key];
            elem.IsReffered = false;
            elem.Parent = null;
            elem.Name = string.Empty;
            this.Value.Remove(key);
            
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name, elem, null);
        }
        
        public void Clear()
        {
            this.Value.Clear();
            this.Children!.Clear();
            
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, null);
        }

        public int Count => this.Value.Count;
    }
}

//
//     public class ShadowRpcDictionary<TK, TV> : ShadowRpcProperty<Dictionary<TK, RpcContainerNode<TV>>>
//         where TK : notnull
//     {
//         public ShadowRpcDictionary(string name) : base(name)
//         {
//             // this.GetType().GetProperty("111").SetMethod.GetMethodBody().
//             Expression<Func<int>> add = () => 1 + 2;
//             var func = add.Compile(); // Create Delegate
//             // func.Method.GetMethodBody()
//         }
//         
//         public TV this[TK key]
//         {
//             get => value_[key].Value;
//             set => throw new Exception("ShadowRpcDictionary is readonly.");
//         }
//         
//         public ReadOnlyDictionary<TK, RpcContainerNode<TV>> AsReadOnly() => new(value_);
//     }
// }