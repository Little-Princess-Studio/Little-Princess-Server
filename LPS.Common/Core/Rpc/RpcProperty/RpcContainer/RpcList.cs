// -----------------------------------------------------------------------
// <copyright file="RpcList.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Rpc.RpcProperty.RpcContainer
{
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using Google.Protobuf;
    using Google.Protobuf.Collections;
    using Google.Protobuf.WellKnownTypes;
    using LPS.Common.Core.Rpc.InnerMessages;
    using LPS.Common.Core.Rpc.RpcPropertySync;
    using Type = System.Type;

    /// <summary>
    /// RpcList, similar with <see cref="List{T}"/> but used as RpcPropertyContainer.
    /// </summary>
    /// <typeparam name="TElem">Type of the element.</typeparam>
    [RpcPropertyContainer]
    public class RpcList<TElem> : RpcPropertyContainer, IList<TElem>, ISyncOpActionSetValue,
        ISyncOpActionAddElem, ISyncOpActionUpdatePair, ISyncOpActionInsertElem, ISyncOpActionRemoveElem,
        ISyncOpActionClear
    {
        /// <summary>
        /// Gets or sets the callback when setting raw value to the list.
        /// </summary>
        public OnSetValueCallBack<List<TElem>>? OnSetValue { get; set; }

        /// <summary>
        /// Gets or sets the callback when updating elem for the list.
        /// </summary>
        public OnUpdateValueCallBack<int, TElem>? OnUpdatePair { get; set; }

        /// <summary>
        /// Gets or sets the callback when appending elem to the list.
        /// </summary>
        public OnAddListElemCallBack<TElem>? OnAddElem { get; set; }

        /// <summary>
        /// Gets or sets the callback when removing elem from the list.
        /// </summary>
        public OnRemoveElemCallBack<int, TElem>? OnRemoveElem { get; set; }

        /// <summary>
        /// Gets or sets the callback when clearing the list.
        /// </summary>
        public OnClearCallBack? OnClear { get; set; }

        /// <summary>
        /// Gets or sets the callback when inserting elem to the list.
        /// </summary>
        public OnInsertItemCallBack<TElem>? OnInsertItem { get; set; }

        private static readonly Type UnpackElemType = typeof(TElem).IsSubclassOf(typeof(RpcPropertyContainer))
            ? typeof(TElem)
            : typeof(RpcPropertyContainer<TElem>);

        static RpcList()
        {
            RpcHelper.RegisterRpcPropertyContainer(typeof(RpcList<TElem>));
        }

        /// <summary>
        /// Entry method for RpcPropertyContainer costume deserialize.
        /// </summary>
        /// <param name="content">Protobuf Any object needed be deserialized to this list.</param>
        /// <returns>Deserialized RpcPropertyContainer object.</returns>
        /// <exception cref="Exception">Throw exception if failed to deserialize.</exception>
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

        private List<RpcPropertyContainer> rawValue;

        /// <summary>
        /// Gets the raw value of the list.
        /// </summary>
        public List<RpcPropertyContainer> RawValue
        {
            get => this.rawValue;
            private set => this.SetValueIntern(value, true, false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcList{TElem}"/> class.
        /// </summary>
        public RpcList()
        {
            this.rawValue = new List<RpcPropertyContainer>();
            this.Children = new Dictionary<string, RpcPropertyContainer>();
        }

        /// <inheritdoc/>
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
            foreach (var @new in targetContainer.rawValue)
            {
                @new.InsertToPropTree(this, $"{++i}", this.TopOwner);
                this.Children![@new.Name!] = @new;
            }
        }

        /// <inheritdoc/>
        public override Any ToRpcArg()
        {
            IMessage? protobufList = null;

            if (this.RawValue.Count > 0)
            {
                protobufList = RpcHelper.RpcContainerListToProtoBufAny(this);
            }
            else
            {
                protobufList = new NullArg();
            }

            return Any.Pack(protobufList);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcList{TElem}"/> class.
        /// </summary>
        /// <param name="size">Size of the list.</param>
        /// <param name="defaultVal">Default value of the list elem.</param>
        public RpcList(int size, [DisallowNull] TElem defaultVal)
        {
            ArgumentNullException.ThrowIfNull(defaultVal);

            this.rawValue = new List<RpcPropertyContainer>(size);

            this.Children = new();

            for (int i = 0; i < size; i++)
            {
                var newContainer = this.HandleValue(defaultVal, i);

                this.RawValue.Add(newContainer);
                this.Children.Add($"{i}", newContainer);
            }
        }

        /// <summary>
        /// Add elem to list.
        /// </summary>
        /// <param name="elem">Raw value of the elem.</param>
        public void Add(TElem elem)
        {
            this.AssertNotShadowPropertyChange();

            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = this.HandleValue(elem, this.RawValue.Count);
            newContainer.UpdateTopOwner(this.TopOwner);

            this.RawValue.Add(newContainer);
            this.Children!.Add(newContainer.Name!, newContainer);
            this.NotifyChange(
                RpcPropertySyncOperation.AddListElem,
                newContainer.Name!,
                newContainer,
                RpcSyncPropertyType.List);
            this.OnAddElem?.Invoke(elem);
        }

        /// <summary>
        /// Insert a elem to list.
        /// </summary>
        /// <param name="index">Index to insert.</param>
        /// <param name="elem">Raw value of the elem.</param>
        public void Insert(int index, TElem elem)
        {
            this.AssertNotShadowPropertyChange();

            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = this.HandleValue(elem, index);
            newContainer.UpdateTopOwner(this.TopOwner);

            this.RawValue.Insert(index, newContainer);
            this.Children!.Add(newContainer.Name!, newContainer);
            this.NotifyChange(
                RpcPropertySyncOperation.InsertElem,
                newContainer.Name!,
                newContainer,
                RpcSyncPropertyType.List);
            this.OnInsertItem?.Invoke(index, elem);
        }

        /// <summary>
        /// Clear the list.
        /// </summary>
        public void Clear()
        {
            this.AssertNotShadowPropertyChange();

            foreach (var (_, container) in this.Children!)
            {
                container.RemoveFromPropTree();
            }

            this.RawValue.Clear();
            this.Children!.Clear();
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, RpcSyncPropertyType.List);
            this.OnClear?.Invoke();
        }

        /// <summary>
        /// Gets the count of the list.
        /// </summary>
        public int Count => this.RawValue.Count;

        /// <summary>
        /// Operator [] of the list.
        /// </summary>
        /// <param name="index">Index of the list.</param>
        public TElem this[int index]
        {
            get => (TElem)this.RawValue[index].GetRawValue();
            set
            {
                this.AssertNotShadowPropertyChange();
                ArgumentNullException.ThrowIfNull(value);
                var old = this.RawValue[index];
                var oldName = old.Name!;

                if (value is RpcPropertyContainer container)
                {
                    old.RemoveFromPropTree();
                    container.InsertToPropTree(this, oldName, this.TopOwner);

                    this.rawValue[index] = container;
                    this.NotifyChange(
                        RpcPropertySyncOperation.UpdatePair,
                        this.RawValue[index].Name!,
                        container,
                        RpcSyncPropertyType.List);
                    this.OnUpdatePair?.Invoke(index, (TElem)old.GetRawValue(), value);
                }
                else
                {
                    var oldWithContainer = (RpcPropertyContainer<TElem>)old;
                    var oldVal = oldWithContainer.Value;
                    oldWithContainer.Set(value, false, false);
                    this.NotifyChange(
                        RpcPropertySyncOperation.UpdatePair,
                        this.RawValue[index].Name!,
                        oldWithContainer,
                        RpcSyncPropertyType.List);
                    this.OnUpdatePair?.Invoke(index, oldVal, value);
                }
            }
        }

        /// <summary>
        /// Generate a copied list with the list's raw values.
        /// </summary>
        /// <returns>Copied list of the list's raw values.</returns>
        public List<TElem> ToCopy() => this.RawValue.Select(e => (TElem)e.GetRawValue()).ToList();

        /// <summary>
        /// Assign this list with another RpcList.
        /// </summary>
        /// <param name="target">Another RpcList.</param>
        /// <exception cref="ArgumentNullException">ArgumentNullException.</exception>
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

        /// <inheritdoc/>
        void ISyncOpActionSetValue.Apply(RepeatedField<Any> args)
        {
            var value = args[0];
            var realValue = RpcHelper.CreateRpcPropertyContainerByType(
                typeof(RpcList<TElem>),
                value) as RpcList<TElem>;

            this.Assign(realValue!);
        }

        /// <inheritdoc/>
        void ISyncOpActionAddElem.Apply(RepeatedField<Any> args)
        {
            var valueType = UnpackElemType;

            foreach (var any in args)
            {
                var realValue = RpcHelper.CreateRpcPropertyContainerByType(valueType, any);

                // TODO: set rpc container
                this.Add((TElem)realValue.GetRawValue());
            }
        }

        /// <inheritdoc/>
        void ISyncOpActionRemoveElem.Apply(RepeatedField<Any> args) => this.RemoveAt(0);

        /// <inheritdoc/>
        void ISyncOpActionClear.Apply() => this.Clear();

        /// <inheritdoc/>
        void ISyncOpActionInsertElem.Apply(RepeatedField<Any> args)
        {
            var valueType = UnpackElemType;

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

        /// <inheritdoc/>
        void ISyncOpActionUpdatePair.Apply(RepeatedField<Any> args)
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

                var valueType = UnpackElemType;

                var realValue = RpcHelper.CreateRpcPropertyContainerByType(
                    valueType,
                    value);

                // TODO: set rpc container
                this[realKey] = (TElem)realValue.GetRawValue();
            }
        }

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public int IndexOf(TElem item)
        {
            for (var i = 0; i < this.rawValue.Count; i++)
            {
                if (item!.Equals(this[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <inheritdoc/>
        public void RemoveAt(int index) => this.RemoveInternal(index);

        /// <inheritdoc/>
        public bool Contains(TElem item) => this.IndexOf(item) != -1;

        /// <inheritdoc/>
        public void CopyTo(TElem[] array, int arrayIndex)
        {
            for (var i = 0; i < this.rawValue.Count; i++)
            {
                array[arrayIndex++] = this[i];
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IEnumerator<TElem> GetEnumerator()
        {
            return new Enumerator<TElem>(this.rawValue);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private class Enumerator<TEnumeratorElem> : IEnumerator<TEnumeratorElem>
        {
            private readonly List<RpcPropertyContainer> list;
            private List<RpcPropertyContainer>.Enumerator enumerator;

            public Enumerator(List<RpcPropertyContainer> list)
            {
                this.enumerator = list.GetEnumerator();
                this.list = list;
            }

            public bool MoveNext() => this.enumerator.MoveNext();

            public void Reset()
            {
                this.enumerator.Dispose();
                this.enumerator = this.list.GetEnumerator();
            }

            object IEnumerator.Current => this.Current!;

            public TEnumeratorElem Current => (TEnumeratorElem)this.enumerator.Current.GetRawValue();

            public void Dispose() => this.enumerator.Dispose();
        }

        private void RemoveInternal(int index)
        {
            this.AssertNotShadowPropertyChange();

            var elem = this.RawValue[index];
            elem.RemoveFromPropTree();

            this.RawValue.RemoveAt(index);
            this.Children!.Remove($"{index}");
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name!, null, RpcSyncPropertyType.List);

            // TODO: set rpc container
            this.OnRemoveElem?.Invoke(index, (TElem)elem.GetRawValue());
        }

        private void SetValueIntern(List<RpcPropertyContainer> value, bool withNotify, bool bySync)
        {
            if (!bySync)
            {
                this.AssertNotShadowPropertyChange();
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
                    this.rawValue = value;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.List);
                    this.OnSetValue.Invoke(old, this.ToCopy());
                }
                else
                {
                    this.rawValue = value;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.List);
                }
            }
            else
            {
                this.rawValue = value;
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
    }
}