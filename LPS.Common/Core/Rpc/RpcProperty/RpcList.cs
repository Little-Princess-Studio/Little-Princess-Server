﻿using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcPropertySync;
using Type = System.Type;

namespace LPS.Common.Core.Rpc.RpcProperty
{
    interface IRpcSyncableList : IRpcSyncableContainer
    {
        void OnAddListElem(Any[] args);
        void OnRemoveElem(Any[] args);
        void OnClear();
        void OnInsertElem(Any[] args);
        void OnUpdatePair(Any[] args);
    }

    [RpcPropertyContainer]
    public class RpcList<TElem> : RpcPropertyContainer, IList<TElem>, IRpcSyncableList
    {
        public OnSetValueCallBack<List<TElem>>? OnSetValue { get; set; }
        public OnUpdateValueCallBack<int, TElem>? OnUpdatePair { get; set; }
        public OnAddListElemCallBack<TElem>? OnAddElem { get; set; }
        public OnRemoveElemCallBack<int, TElem>? OnRemoveElem { get; set; }
        public OnClearCallBack? OnClear { get; set; }
        public OnInsertItemCallBack<TElem>? OnInsertItem { get; set; }

        private static readonly Type UnpackElemType_ = typeof(TElem).IsSubclassOf(typeof(RpcPropertyContainer))
            ? typeof(TElem)
            : typeof(RpcPropertyContainer<TElem>);
        
        static RpcList()
        {
            RpcHelper.RegisterRpcPropertyContainer(typeof(RpcList<TElem>));
        }

        private List<RpcPropertyContainer> rawValue_;

        public List<RpcPropertyContainer> RawValue
        {
            get => rawValue_;
            private set => this.SetValueIntern(value, true, false);
        }

        private void SetValueIntern(List<RpcPropertyContainer> value, bool withNotify, bool bySync)
        {
            if (!bySync)
            {
                AssertNotShadowPropertyChange();
            }

            ArgumentNullException.ThrowIfNull(value);

            if (withNotify)
            {
                foreach (var old in this.RawValue)
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


            if (withNotify)
            {
                if (this.OnSetValue != null)
                {
                    var old = this.ToCopy();
                    this.rawValue_ = value;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.List);
                    this.OnSetValue.Invoke(old, this.ToCopy());
                }
                else
                {
                    this.rawValue_ = value;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.List);
                }
            }
            else
            {
                this.rawValue_ = value;
            }
        }

        public RpcList()
        {
            this.rawValue_ = new List<RpcPropertyContainer>();
            this.Children = new Dictionary<string, RpcPropertyContainer>();
        }

        public override void AssignInternal(RpcPropertyContainer target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.GetType() != typeof(RpcList<TElem>))
            {
                throw new Exception("Cannot apply assign between different types.");
            }

            var targetContainer = (target as RpcList<TElem>)!;
            targetContainer.RemoveFromPropTree();

            int i = 0;
            foreach (var @new in targetContainer.rawValue_)
            {
                @new.InsertToPropTree(this, $"{++i}", this.TopOwner);
                this.Children![@new.Name!] = @new;
            }
        }

        public override Any ToRpcArg()
        {
            IMessage? pbList = null;

            if (this.RawValue.Count > 0)
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
                        .Select((e, i) =>
                        {
                            var container = RpcHelper.CreateRpcPropertyContainerByType(typeof(TElem), e);
                            container.Name = $"{i}";
                            container.IsReferred = true;
                            return container;
                        })
                        .ToList();

                    RpcList<TElem> propList = new();
                    propList.SetValueIntern(rawList, true, true);

                    return propList;
                }
                else
                {
                    List<RpcPropertyContainer> rawList = rpcList.PayLoad
                        .Select((e, i) =>
                        {
                            var container =
                                RpcHelper.CreateRpcPropertyContainerByType(typeof(RpcPropertyContainer<TElem>), e);
                            container.Name = $"{i}";
                            container.IsReferred = true;
                            return container;
                        })
                        .ToList();

                    RpcList<TElem> propList = new();
                    propList.SetValueIntern(rawList, true, true);

                    return propList;
                }
            }

            if (content.Is(NullArg.Descriptor))
            {
                return new RpcList<TElem>();
            }

            throw new Exception($"Invalid list content: {content}");
        }

        public RpcList(int size, [DisallowNull] TElem defaultVal)
        {
            ArgumentNullException.ThrowIfNull(defaultVal);

            this.rawValue_ = new List<RpcPropertyContainer>(size);

            this.Children = new();

            for (int i = 0; i < size; i++)
            {
                var newContainer = HandleValue(defaultVal, i);

                this.RawValue.Add(newContainer);
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

        public void Add(TElem elem)
        {
            AssertNotShadowPropertyChange();

            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = HandleValue(elem, this.RawValue.Count);
            newContainer.UpdateTopOwner(this.TopOwner);

            this.RawValue.Add(newContainer);
            this.Children!.Add(newContainer.Name!, newContainer);
            this.NotifyChange(RpcPropertySyncOperation.AddListElem, newContainer.Name!, newContainer,
                RpcSyncPropertyType.List);
            this.OnAddElem?.Invoke(elem);
        }

        public void RemoveInternal(int index)
        {
            AssertNotShadowPropertyChange();

            var elem = this.RawValue[index];
            elem.RemoveFromPropTree();

            this.RawValue.RemoveAt(index);
            this.Children!.Remove($"{index}");
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name!, null, RpcSyncPropertyType.List);
            // TODO: set rpc container
            this.OnRemoveElem?.Invoke(index, (TElem) elem.GetRawValue());
        }

        public void Insert(int index, TElem elem)
        {
            AssertNotShadowPropertyChange();

            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = HandleValue(elem, index);
            newContainer.UpdateTopOwner(this.TopOwner);

            this.RawValue.Insert(index, newContainer);
            this.Children!.Add(newContainer.Name!, newContainer);
            this.NotifyChange(RpcPropertySyncOperation.InsertElem, newContainer.Name!,
                newContainer, RpcSyncPropertyType.List);
            this.OnInsertItem?.Invoke(index, elem);
        }

        public void Clear()
        {
            AssertNotShadowPropertyChange();

            foreach (var (_, container) in this.Children!)
            {
                container.RemoveFromPropTree();
            }

            this.RawValue.Clear();
            this.Children!.Clear();
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, RpcSyncPropertyType.List);
            this.OnClear?.Invoke();
        }

        public int Count => this.RawValue.Count;

        public TElem this[int index]
        {
            get => (TElem) this.RawValue[index].GetRawValue();
            set
            {
                AssertNotShadowPropertyChange();
                ArgumentNullException.ThrowIfNull(value);
                var old = this.RawValue[index];
                var oldName = old.Name!;

                if (value is RpcPropertyContainer container)
                {
                    old.RemoveFromPropTree();
                    container.InsertToPropTree(this, oldName, this.TopOwner);

                    this.rawValue_[index] = container;
                    this.NotifyChange(RpcPropertySyncOperation.UpdatePair, this.RawValue[index].Name!,
                        container, RpcSyncPropertyType.List);
                    this.OnUpdatePair?.Invoke(index, (TElem) old.GetRawValue(), value);
                }
                else
                {
                    var oldWithContainer = (RpcPropertyContainer<TElem>) old;
                    var oldVal = oldWithContainer.Value;
                    oldWithContainer.Set(value, false, false);
                    this.NotifyChange(RpcPropertySyncOperation.UpdatePair, this.RawValue[index].Name!,
                        oldWithContainer, RpcSyncPropertyType.List);
                    this.OnUpdatePair?.Invoke(index, oldVal, value);
                }
            }
        }

        public List<TElem> ToCopy() => this.RawValue.Select(e => (TElem) e.GetRawValue()).ToList();

        public void Assign(RpcList<TElem> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target == this)
            {
                return;
            }

            this.OnSetValue?.Invoke(this.ToCopy(), target.ToCopy());
            this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, target, RpcSyncPropertyType.List);

            this.AssignInternal(target);
        }

        public bool IsReadOnly => false;

        public int IndexOf(TElem item)
        {
            for (var i = 0; i < rawValue_.Count; i++)
            {
                if (item!.Equals(this[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        public void RemoveAt(int index) => this.RemoveInternal(index);

        public bool Contains(TElem item) => this.IndexOf(item) != -1;

        public void CopyTo(TElem[] array, int arrayIndex)
        {
            for (var i = 0; i < this.rawValue_.Count; i++)
            {
                array[arrayIndex++] = this[i];
            }
        }

        public bool Remove(TElem item)
        {
            var index = this.IndexOf(item);
            if (index != -1)
            {
                this.RemoveAt(index);
                return true;
            }

            return false;
        }

        public IEnumerator<TElem> GetEnumerator()
        {
            return new Enumerator<TElem>(rawValue_);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class Enumerator<TEnumeratorElem> : IEnumerator<TEnumeratorElem>
        {
            private readonly List<RpcPropertyContainer> list_;
            private List<RpcPropertyContainer>.Enumerator enumerator_;

            public Enumerator(List<RpcPropertyContainer> list)
            {
                enumerator_ = list.GetEnumerator();
                list_ = list;
            }

            public bool MoveNext() => enumerator_.MoveNext();

            public void Reset()
            {
                enumerator_.Dispose();
                enumerator_ = list_.GetEnumerator();
            }

            object IEnumerator.Current => Current!;

            public TEnumeratorElem Current => (TEnumeratorElem) enumerator_.Current.GetRawValue();

            public void Dispose() => enumerator_.Dispose();
        }

        void IRpcSyncableContainer.OnSetValue(Any[] args)
        {
            var value = args[0];
            var realValue = RpcHelper.CreateRpcPropertyContainerByType(
                typeof(RpcList<TElem>),
                value) as RpcList<TElem>;

            this.Assign(realValue!);
        }

        void IRpcSyncableList.OnAddListElem(Any[] args)
        {
            var valueType = UnpackElemType_;

            foreach (var any in args)
            {
                var realValue = RpcHelper.CreateRpcPropertyContainerByType(valueType, any);
                
                // TODO: set rpc container
                this.Add((TElem) realValue.GetRawValue());
            }
        }

        void IRpcSyncableList.OnRemoveElem(Any[] args) => this.RemoveAt(0);

        void IRpcSyncableList.OnClear() => this.Clear();

        void IRpcSyncableList.OnInsertElem(Any[] args)
        {
            var valueType = UnpackElemType_;

            foreach (var any in args)
            {
                if (!any.Is(PairWithIntKey.Descriptor))
                {
                    var pair = any.Unpack<PairWithIntKey>();
                    var index = pair.Key;
                    var value = RpcHelper.CreateRpcPropertyContainerByType(valueType, pair.Value);
                    
                    // TODO: set rpc container
                    this.Insert(index, (TElem)value.GetRawValue());
                }
                else
                {
                    throw new Exception("Invalid list insert elem");
                }
            }
        }

        void IRpcSyncableList.OnUpdatePair(Any[] args)
        {
            var updateDict = args[0];

            if (!updateDict.Is(DictWithIntKeyArg.Descriptor))
            {
                throw new Exception("Invalid update dict protobuf content.");
            }

            var dict = updateDict.Unpack<DictWithIntKeyArg>();
            
            foreach (var (key, value) in dict.PayLoad)
            {
                var realKey = key;
            
                var valueType = UnpackElemType_;

                var realValue = RpcHelper.CreateRpcPropertyContainerByType(
                    valueType,
                    value);
                
                // TODO: set rpc container
                this[realKey] = (TElem) realValue.GetRawValue();
            }
        }
    }
}