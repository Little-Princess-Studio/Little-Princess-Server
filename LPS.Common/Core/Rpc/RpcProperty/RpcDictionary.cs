// using System.Collections.ObjectModel;
// using System.Linq.Expressions;
//

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
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
                    old.RemoveFromPropTree();
                }

                this.Children!.Clear();
                foreach (var (k, @new) in value)
                {
                    @new.InsertToPropTree(this, $"{k}", this.TopOwner);
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

        public override Any ToRpcArg()
        {
            IMessage? pbDictVal = null;

            if (this.Value.Count > 0)
            {
                pbDictVal = RpcHelper.RpcContainerDictToProtoBufAny(this);
            }
            
            var pbRpc = new DictWithStringKeyArg();
            pbRpc.PayLoad.Add("value", pbDictVal == null ? Any.Pack(new NullArg()) : Any.Pack(pbDictVal));

            return Any.Pack(pbRpc);
        }

        public override void FromRpcArg(Any content)
        {
            
        }

        public TV this[TK key]
        {
            get { return (TV) this.Value[key].GetRawValue(); }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                RpcPropertyContainer? old = null;

                if (this.Value.ContainsKey(key))
                {
                    old = this.Value[key];
                }

                if (value is RpcPropertyContainer container)
                {
                    if (old != null)
                    {
                        old.RemoveFromPropTree();
                    }

                    container.InsertToPropTree(this, $"{key}", this.TopOwner);

                    this.Value[key] = container;
                    this.Children![container.Name!] = container;
                    this.NotifyChange(RpcPropertySyncOperation.UpdateDict, container.Name!, old != null
                            ? old.GetRawValue()
                            : null,
                        container.GetRawValue());
                }
                else
                {
                    if (old == null)
                    {
                        var newContainer = new RpcPropertyContainer<TV>(value)
                        {
                            Parent = this,
                            Name = $"{key}",
                            IsReferred = true,
                        };
                        newContainer.UpdateTopOwner(this.TopOwner);

                        this.Value[key] = newContainer;
                        this.Children![newContainer.Name] = newContainer;
                        this.NotifyChange(RpcPropertySyncOperation.UpdateDict, newContainer.Name!, old != null
                                ? old.GetRawValue()
                                : null,
                            newContainer.GetRawValue());
                    }
                    else // reuse old
                    {
                        var oldWithType = (RpcPropertyContainer<TV>) old;
                        var oldVal = oldWithType.GetRawValue();
                        oldWithType.Value = value;
                        this.NotifyChange(
                            RpcPropertySyncOperation.UpdateDict,
                            old.Name!, oldVal, oldWithType.GetRawValue());
                    }
                }
            }
        }

        public void Remove(TK key)
        {
            var elem = this.Value[key];
            elem.RemoveFromPropTree();

            this.Value.Remove(key);
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name, elem.GetRawValue(), null);
        }

        public void Clear()
        {
            foreach (var (_, value) in this.Value)
            {
                value.RemoveFromPropTree();
            }

            this.Value.Clear();
            this.Children!.Clear();

            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, null);
        }

        public Dictionary<TK, TV> ToCopy() =>
            this.Value.ToDictionary(
                pair => pair.Key,
                pair => (TV) pair.Value.GetRawValue());
        
        public int Count => this.Value.Count;
    }
}