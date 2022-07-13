using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcList<TElem> : RpcPropertyContainer
    {
        private List<RpcPropertyContainer> value_;
        public List<RpcPropertyContainer> Value
        {
            get => value_;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                
                foreach (var old in this.Value)
                {
                    old.Parent = null;
                    old.IsReferred = false;
                    old.UpdateTopOwner(null);   
                }

                this.Children!.Clear();
                foreach (var @new in value)
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
        
        public RpcList()
        {
            this.value_ = new List<RpcPropertyContainer>();
            this.Children = new Dictionary<string, RpcPropertyContainer>();
        }

        public override object GetRawValue()
        {
            return this.value_;
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

            this.value_ = new List<RpcPropertyContainer>(size);
            
            this.Children = new();

            for (int i = 0; i < size; i++)
            {
                var newContainer = HandleValue(defaultVal, i);

                this.Value[i] = newContainer;
                this.Children.Add($"{i}", newContainer);
            }
        }

        private RpcPropertyContainer HandleValue([DisallowNull] TElem defaultVal, int index)
        {
            RpcPropertyContainer newContainer;
            if (defaultVal is RpcPropertyContainer defaultValAsContainer)
            {
                newContainer = defaultValAsContainer;
                newContainer.Name = "${index}";
                newContainer.Parent = this;
                newContainer.IsReferred = true;
            }
            else
            {
                newContainer = new RpcPropertyContainer<TElem>(defaultVal)
                {
                    Value = defaultVal,
                    Parent = this,
                    Name = "${index}",
                    IsReferred = true,
                };
            }

            return newContainer;
        }

        public void Add([DisallowNull] TElem elem)
        {
            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = HandleValue(elem, this.Value.Count);
            newContainer.UpdateTopOwner(this.TopOwner);
            
            this.Value.Add(newContainer);
            this.Children!.Add(newContainer.Name!, newContainer);
            this.NotifyChange(RpcPropertySyncOperation.AddListElem, newContainer.Name!, null, elem);
        }

        public void RemoveAt(int index)
        {
            var elem = this.Value[index];
            elem.IsReferred = false;
            elem.Parent = null;
            elem.Name = string.Empty;
            elem.UpdateTopOwner(null);
            
            this.Value.RemoveAt(index);
            this.Children!.Remove($"{index}");
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name, elem, null);
        }

        public void Insert(int index, [DisallowNull] TElem elem)
        {
            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = HandleValue(elem, index);
            newContainer.UpdateTopOwner(this.TopOwner);
            
            this.Value.Insert(index, newContainer);
            this.Children!.Add(newContainer.Name!, newContainer);
            this.NotifyChange(RpcPropertySyncOperation.InsertElem, newContainer.Name!, null, elem);
        }

        public void Clear()
        {
            foreach (var (_, container) in this.Children!)
            {
                container.Parent = null;
                container.Name = string.Empty;
                container.IsReferred = false;
                container.UpdateTopOwner(null);
            }
            
            this.Value.Clear();
            this.Children!.Clear();
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, null);
        }

        public int Count => this.Value.Count;
        
        public TElem this[int index]
        {
            get
            {
                //todo: how can we remove this `if`?
                if (typeof(TElem).IsSubclassOf(typeof(RpcPropertyContainer)))
                {
                    return (TElem)this.Value[index].GetRawValue();
                }

                return ((RpcPropertyContainer<TElem>) this.Value[index]).Value;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var old = this.Value[index];
                var oldName = old.Name!;

                if (value is RpcPropertyContainer container)
                {
                    old.Parent = null;
                    old.Name = string.Empty;
                    old.IsReferred = false;
                    old.UpdateTopOwner(null);
                    
                    this.value_[index] = container;
                    container.Parent = this;
                    container.Name = oldName;
                    container.IsReferred = true;
                    container.UpdateTopOwner(this.TopOwner);
                    
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Value[index].Name!, old, value);
                }
                else
                {
                    var oldWithContainer = (RpcPropertyContainer<TElem>)old;
                    var oldVal = oldWithContainer.Value;
                    oldWithContainer.SetWithoutNotify(value);
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Value[index].Name!, oldVal, value);
                }

            }
        }
    }
}
