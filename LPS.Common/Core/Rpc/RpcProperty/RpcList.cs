using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcList<TElem> : RpcPropertyContainer
    {
        private List<RpcPropertyContainer<TElem>> value_;
        public List<RpcPropertyContainer<TElem>> Value
        {
            get => value_;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name, old: value_!, value);
                this.value_ = value;
            }
        }
        
        public RpcList()
        {
            this.value_ = new List<RpcPropertyContainer<TElem>>();
            this.Children = new();
        }

        public override Any ToRpcArg()
        {
            IMessage? pbList = null;
            DictWithStringKeyArg? pbChildren = null;

            if (this.Value.Count > 0)
            {
                pbList = RpcHelper.RpcContainerListToProtoBufAny(this);
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
            pbRpc.PayLoad.Add("value", pbList == null ? Any.Pack(new NullArg()) : Any.Pack(pbList));
            pbRpc.PayLoad.Add("children", pbChildren == null ? Any.Pack(new NullArg()) : Any.Pack(pbChildren));

            return Any.Pack(pbRpc);
        }

        public RpcList(int size, [DisallowNull] TElem defaultVal)
        {
            ArgumentNullException.ThrowIfNull(defaultVal);

            this.value_ = new List<RpcPropertyContainer<TElem>>(size);
            
            this.Children = new();

            for (int i = 0; i < size; i++)
            {
                var newContainer = new RpcPropertyContainer<TElem>(defaultVal)
                {
                    Parent = this,
                    Name = "${i}",
                };

                this.HandleIfContainer<TElem>(newContainer, defaultVal);
                this.Value[i] = newContainer;
                
                this.Children.Add($"{i}", newContainer);
            }
        }

        public void Add([DisallowNull] TElem elem)
        {
            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = new RpcPropertyContainer<TElem>(elem)
            {
                Parent = this,
                Name = $"{this.Value.Count}",
                IsReffered = true,
            };
            
            this.HandleIfContainer<TElem>(newContainer, elem);
            this.Value.Add(newContainer);
            this.NotifyChange(RpcPropertySyncOperation.AddListElem, newContainer.Name, null, elem);
            
            this.Children!.Add(newContainer.Name, newContainer);
        }

        public void RemoveAt(int index)
        {
            var elem = this.Value[index];
            elem.IsReffered = false;
            elem.Parent = null;
            elem.Name = string.Empty;
            this.Value.RemoveAt(index);
            
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name, elem, null);
        }

        public void InsertAt(int index, [DisallowNull] TElem elem)
        {
            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = new RpcPropertyContainer<TElem>(elem)
            {
                Parent = this,
                Name = $"{index}",
                IsReffered = true,
            };
            
            this.HandleIfContainer<TElem>(newContainer, elem);
            this.Value.Insert(index, newContainer);
            this.NotifyChange(RpcPropertySyncOperation.InsertElem, newContainer.Name, null, elem);
            
            this.Children!.Add(newContainer.Name, newContainer);
        }

        public void Clear()
        {
            this.Value.Clear();
            this.Children!.Clear();
            
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, null);
        }

        public int Count => this.Value.Count;
        
        public TElem this[int index]
        {
            get => this.Value[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                this.HandleIfContainer<TElem>(this.Value[index], value);

                var old = this.Value[index].Value!;
                if (old is RpcPropertyContainer oldContainer)
                {
                    oldContainer.IsReffered = false;
                }

                this.Value[index].SetWithoutNotify(value);
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Value[index].Name!, old, value);
            }
        }
    }

    // public class ShadowRpcList<TElem> : List<RpcContainerNode<TElem>>
    // {
    //     public ShadowRpcList()
    //     {
    //     }
    // }
}
