using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc.RpcProperty
{
    [RpcPropertyContainer]
    public class RpcList<TElem> : RpcPropertyContainer
    {
        static RpcList()
        {
            RpcHelper.RegisterRpcPropertyContainer(typeof(RpcList<TElem>));
        }

        private List<RpcPropertyContainer> value_;

        public List<RpcPropertyContainer> Value
        {
            get => value_;
            set => this.SetValueIntern(value, true);
        }

        private void SetValueIntern(List<RpcPropertyContainer> value, bool withNotify)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (withNotify)
            {
                foreach (var old in this.Value)
                {
                    old.RemoveFromPropTree();
                }
            }

            this.Children!.Clear();

            int i = 0;

            if (withNotify)
            {
                foreach (var @new in value)
                {
                    @new.InsertToPropTree(this, $"{++i}", this.TopOwner);
                    this.Children[@new.Name!] = @new;
                }
            }
            else
            {
                foreach (var @new in value)
                {
                    @new.Name = $"{++i}";
                    this.Children[@new.Name!] = @new;
                }
            }

            this.value_ = value;

            if (withNotify)
            {
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, old: value_, value);
            }
        }

        public RpcList()
        {
            this.value_ = new List<RpcPropertyContainer>();
            this.Children = new Dictionary<string, RpcPropertyContainer>();
        }

        public override Any ToRpcArg()
        {
            IMessage? pbList = null;

            if (this.Value.Count > 0)
            {
                pbList = RpcHelper.RpcContainerListToProtoBufAny(this);
            }
            else
            {
                pbList = new NullArg();
            }

            return Any.Pack(pbList);
        }

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer FromRpcArg(Any content)
        {
            if (content.Is(ListArg.Descriptor))
            {
                var rpcList = content.Unpack<ListArg>();

                if (typeof(TElem).IsSubclassOf(typeof(RpcPropertyContainer)))
                {
                    List<RpcPropertyContainer> rawList = rpcList.PayLoad
                        .Select(e => RpcHelper.CreateRpcPropertyContainerByType(typeof(TElem), e))
                        .ToList();

                    RpcList<TElem> propList = new();
                    propList.SetValueIntern(rawList, false);

                    return propList;
                }
                else
                {
                    List<RpcPropertyContainer> rawList = rpcList.PayLoad
                        .Select(e => RpcHelper.CreateRpcPropertyContainerByType(typeof(RpcPropertyContainer<TElem>), e))
                        .ToList();

                    RpcList<TElem> propList = new();
                    propList.SetValueIntern(rawList, false);

                    return propList;
                }
            }

            throw new Exception($"Invalid list content: {content}");
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
                newContainer.Name = $"{index}";
                newContainer.Parent = this;
                newContainer.IsReferred = true;
            }
            else
            {
                newContainer = new RpcPropertyContainer<TElem>(defaultVal)
                {
                    Value = defaultVal,
                    Parent = this,
                    Name = $"{index}",
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
            elem.RemoveFromPropTree();

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
                container.RemoveFromPropTree();
            }

            this.Value.Clear();
            this.Children!.Clear();
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, null);
        }

        public int Count => this.Value.Count;

        public TElem this[int index]
        {
            get => (TElem) this.Value[index].GetRawValue();
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var old = this.Value[index];
                var oldName = old.Name!;

                if (value is RpcPropertyContainer container)
                {
                    old.RemoveFromPropTree();
                    container.InsertToPropTree(this, oldName, this.TopOwner);

                    this.value_[index] = container;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Value[index].Name!, old, value);
                }
                else
                {
                    var oldWithContainer = (RpcPropertyContainer<TElem>) old;
                    var oldVal = oldWithContainer.Value;
                    oldWithContainer.SetWithoutNotify(value);
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Value[index].Name!, oldVal, value);
                }
            }
        }

        List<TElem> ToCopy() => this.Value.Select(e => (TElem) e.GetRawValue()).ToList();
    }
}