// using System.Collections.ObjectModel;
// using System.Linq.Expressions;
//

using System.Collections.ObjectModel;
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
        private Dictionary<TK, RpcPropertyContainer> value_;

        public Dictionary<TK, RpcPropertyContainer> Value
        {
            get => value_;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                
                foreach (var (_, old) in this.Value)
                {
                    old.Parent = null;
                    old.IsReferred = false;
                    old.UpdateTopOwner(null);   
                }

                this.Children!.Clear();
                foreach (var (_, @new) in value)
                {
                    @new.Parent = this;
                    @new.IsReferred = true;
                    @new.UpdateTopOwner(this.TopOwner);
                    this.Children[@new.Name!] = @new;
                }
                
                this.value_ = value;
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, old: value_!, value);
            }
        }

        static RpcDictionary()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidKeyType<TK>();
        }

        public RpcDictionary()
        {
            this.value_ = new();
            this.Children = new();
        }

        public override object GetRawValue()
        {
            return this.value_;
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
            get
            {
                //todo: how can we remove this `if`?
                if (typeof(TV).IsSubclassOf(typeof(RpcPropertyContainer)))
                {
                    return (TV)this.Value[key].GetRawValue();
                }

                return ((RpcPropertyContainer<TV>) this.Value[key]).Value;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (this.Value.ContainsKey(key))
                {
                    var old = this.Value[key].Value;
                    if (old is RpcPropertyContainer container)
                    {
                        container.IsReferred = false;
                        container.UpdateTopOwner(null);
                    }

                    var val = this.Value[key];
                    val.SetWithoutNotify(value);
                    val.UpdateTopOwner(this.TopOwner);
                    this.NotifyChange(RpcPropertySyncOperation.UpdateDict, val.Name!, old!, value);
                }
                else
                {
                    var newContainer = new RpcPropertyContainer<TV>(value)
                    {
                        Parent = this,
                        Name = $"{key}",
                        IsReferred = true,
                    };
                    
                    this.Value[key] = newContainer;
                    this.NotifyChange(RpcPropertySyncOperation.UpdateDict, newContainer.Name, null, value);

                    this.Children!.Add($"{key}", newContainer);
                }
            }
        }

        public void Remove(TK key)
        {
            var elem = this.Value[key];
            elem.IsReferred = false;
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

        public ReadOnlyDictionary<TK, RpcPropertyContainer<TV>> AsReadOnly() => new(value_);
        
        public int Count => this.Value.Count;
    }
}
