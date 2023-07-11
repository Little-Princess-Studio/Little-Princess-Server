// -----------------------------------------------------------------------
// <copyright file="RpcDictionary.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty.RpcContainer;

using System.Collections;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// pcList, similar with <see cref="Dictionary{TKey,TValue}"/> but used as RpcPropertyContainer.
/// </summary>
/// <typeparam name="TK">Type of the key.</typeparam>
/// <typeparam name="TV">Type of the value.</typeparam>
[RpcPropertyContainer]
public class RpcDictionary<TK, TV> : RpcPropertyContainer, IDictionary<TK, TV>,
    ISyncOpActionSetValue, ISyncOpActionUpdatePair, ISyncOpActionRemoveElem, ISyncOpActionClear
    where TK : notnull
{
    private Dictionary<TK, RpcPropertyContainer> rawValue;

    /// <summary>
    /// Gets or sets the callback when setting raw value to the dict.
    /// </summary>
    public OnSetValueCallBack<Dictionary<TK, TV>>? OnSetValue { get; set; }

    /// <summary>
    /// Gets or sets the callback when updating k-v pair for the list.
    /// </summary>
    public OnUpdateValueCallBack<TK, TV?>? OnUpdatePair { get; set; }

    /// <summary>
    /// Gets or sets the callback when removing elem from the list.
    /// </summary>
    public OnRemoveElemCallBack<TK, TV>? OnRemoveElem { get; set; }

    /// <summary>
    /// Gets or sets the callback when clearing the list.
    /// </summary>
    public OnClearCallBack? OnClear { get; set; }

    private static readonly System.Type UnpackValueType = typeof(TV).IsSubclassOf(typeof(RpcPropertyContainer))
        ? typeof(TV)
        : typeof(RpcPropertyContainer<TV>);

    /// <summary>
    /// Entry method for RpcPropertyContainer costume deserialize.
    /// </summary>
    /// <param name="content">Protobuf Any object needed be deserialized to this dict.</param>
    /// <returns>Deserialized RpcPropertyContainer object.</returns>
    /// <exception cref="Exception">Throw exception if failed to deserialize.</exception>
    [RpcPropertyContainerDeserializeEntry]
    public static RpcPropertyContainer FromRpcArg(Any content)
    {
        if (content.Is(NullArg.Descriptor))
        {
            return new RpcDictionary<TK, TV>();
        }

        var valueType = UnpackValueType;

        if (content.Is(DictWithStringKeyArg.Descriptor) && typeof(string) == typeof(TK))
        {
            var payload = content.Unpack<DictWithStringKeyArg>().PayLoad;

            RpcDictionary<string, TV> rpcDict = new();
            Dictionary<string, RpcPropertyContainer> rawDict = new();

            foreach (var (key, value) in payload)
            {
                var val = RpcHelper.CreateRpcPropertyContainerByType(valueType, value!);
                val.Name = key;
                val.IsReferred = true;
                rawDict[key!] = val;
            }

            rpcDict.SetValueInternal(rawDict, true, true);
            return rpcDict;
        }

        if (content.Is(DictWithIntKeyArg.Descriptor) && typeof(int) == typeof(TK))
        {
            var payload = content.Unpack<DictWithIntKeyArg>().PayLoad;

            RpcDictionary<int, TV> rpcDict = new();
            Dictionary<int, RpcPropertyContainer> rawDict = new();

            foreach (var (key, value) in payload)
            {
                var val = RpcHelper.CreateRpcPropertyContainerByType(valueType, value!);
                val.Name = $"{key}";
                val.IsReferred = true;
                rawDict[key!] = val;
            }

            rpcDict.SetValueInternal(rawDict, true, true);
            return rpcDict;
        }

        if (content.Is(DictWithMailBoxKeyArg.Descriptor) && typeof(MailBox) == typeof(TK))
        {
            var payload = content.Unpack<DictWithMailBoxKeyArg>().PayLoad;

            RpcDictionary<MailBox, TV> rpcDict = new();
            Dictionary<MailBox, RpcPropertyContainer> rawDict = new();

            foreach (var pair in payload)
            {
                var key = RpcHelper.PbMailBoxToRpcMailBox(pair.Key);
                var val = RpcHelper.CreateRpcPropertyContainerByType(valueType, pair.Value);
                val.Name = $"{key}";
                val.IsReferred = true;
                rawDict[key] = val;
            }

            rpcDict.SetValueInternal(rawDict, true, true);
            return rpcDict;
        }

        throw new Exception($"Invalid dict content : {content}");
    }

    /// <summary>
    /// Gets the raw value of the list.
    /// </summary>
    public Dictionary<TK, RpcPropertyContainer> RawValue
    {
        get => this.rawValue;
        private set => this.SetValueInternal(value, true, false);
    }

    static RpcDictionary()
    {
        RpcGenericArgTypeCheckHelper.AssertIsValidKeyType<TK>();
        RpcGenericArgTypeCheckHelper.AssertIsValidValueType<TK>();
        RpcHelper.RegisterRpcPropertyContainer(typeof(RpcDictionary<TK, TV>));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcDictionary{TK, TV}"/> class.
    /// </summary>
    public RpcDictionary()
    {
        this.rawValue = new();
        this.Children = new();
    }

    /// <inheritdoc/>
    public override void AssignInternal(RpcPropertyContainer target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (target.GetType() != typeof(RpcDictionary<TK, TV>))
        {
            throw new Exception("Cannot apply assign between different types.");
        }

        var targetContainer = (target as RpcDictionary<TK, TV>)!;
        targetContainer.RemoveFromPropTree();

        foreach (var (k, @new) in targetContainer.rawValue)
        {
            @new.InsertToPropTree(this, $"{k}", this.TopOwner);
            this.Children![@new.Name!] = @new;
        }
    }

    /// <inheritdoc/>
    public override Any ToRpcArg()
    {
        IMessage? protobufDictVal;

        if (this.RawValue.Count > 0)
        {
            protobufDictVal = RpcHelper.RpcContainerDictToProtoBufAny(this);
        }
        else
        {
            protobufDictVal = new NullArg();
        }

        return Any.Pack(protobufDictVal);
    }

    /// <summary>
    /// Operator [] of the dict.
    /// </summary>
    /// <param name="key">Key.</param>
    public TV this[TK key]
    {
        get => (TV)this.RawValue[key].GetRawValue();
        set => this.IndexSetInternal(key, value, true);
    }

    /// <summary>
    /// Clear the dict.
    /// </summary>
    public void Clear() => this.ClearInternal(true);

    /// <summary>
    /// Assign this dict with another RpcDictionary.
    /// </summary>
    /// <param name="target">Another RpcDictionary.</param>
    /// <exception cref="ArgumentNullException">ArgumentNullException.</exception>
    public void Assign(RpcDictionary<TK, TV> target) => this.AssignInternal(target, true);

    /// <summary>
    /// Generate a copied dict with the dict's raw values.
    /// </summary>
    /// <returns>Copied dict with the list's raw values.</returns>
    public Dictionary<TK, TV> ToCopy() =>
        this.RawValue.ToDictionary(
            pair => pair.Key,
            pair => (TV)pair.Value.GetRawValue());

    /// <summary>
    /// Gets the count of the list.
    /// </summary>
    public int Count => this.RawValue.Count;

    /// <inheritdoc/>
    public bool TryGetValue(TK key, out TV value)
    {
        if (this.RawValue.ContainsKey(key))
        {
            value = (TV)this.rawValue[key].GetRawValue();
            return true;
        }

#pragma warning disable CS8601 // A default expression introduces a null value for a type parameter.
        value = default;
#pragma warning restore CS8601 // A default expression introduces a null value for a type parameter.
        return false;
    }

    /// <inheritdoc/>
    public ICollection<TK> Keys => this.rawValue.Keys;

    /// <inheritdoc/>
    public ICollection<TV> Values => this.rawValue.Values.Select(v => (TV)v.GetRawValue()).ToArray();

    /// <inheritdoc/>
    public void Add(TK key, TV value) => this[key] = value;

    /// <inheritdoc/>
    public bool ContainsKey(TK key) => this.rawValue.ContainsKey(key);

    /// <inheritdoc/>
    public bool Remove(TK key)
    {
        if (this.ContainsKey(key))
        {
            this.RemoveInternal(key, true);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<TK, TV> item) => this[item.Key] = item.Value;

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TK, TV> item)
    {
        return this.ContainsKey(item.Key) && this[item.Key]!.Equals(item.Value);
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
    {
        var keys = this.Keys.ToArray();
        for (int i = 0; i < this.Count; ++i)
        {
            array[arrayIndex++] = new KeyValuePair<TK, TV>(keys[i], this[keys[i]]);
        }
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TK, TV> item)
    {
        if (this.Contains(item))
        {
            this.RemoveInternal(item.Key, true);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
    {
        return new Enumerator(this.rawValue);
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    private class Enumerator : IEnumerator<KeyValuePair<TK, TV>>
    {
        private readonly Dictionary<TK, RpcPropertyContainer> dictionary;
        private Dictionary<TK, RpcPropertyContainer>.Enumerator enumerator;

        public Enumerator(Dictionary<TK, RpcPropertyContainer> dictionary)
        {
            this.enumerator = dictionary.GetEnumerator();
            this.dictionary = dictionary;
        }

        public bool MoveNext() => this.enumerator.MoveNext();

        public void Reset()
        {
            this.enumerator.Dispose();
            this.enumerator = this.dictionary.GetEnumerator();
        }

        public KeyValuePair<TK, TV> Current => new KeyValuePair<TK, TV>(
            this.enumerator.Current.Key,
            (TV)this.enumerator.Current.Value.GetRawValue());

        object IEnumerator.Current => this.Current;

        public void Dispose() => this.enumerator.Dispose();
    }

    /// <inheritdoc/>
    void ISyncOpActionSetValue.Apply(RepeatedField<Any> args)
    {
        var value = args[0];

        var realValue = RpcHelper.CreateRpcPropertyContainerByType(
            typeof(RpcDictionary<TK, TV>),
            value) as RpcDictionary<TK, TV>;

        this.AssignInternal(realValue!, false);
    }

    /// <inheritdoc/>
    void ISyncOpActionUpdatePair.Apply(RepeatedField<Any> args)
    {
        var updateDict = args[0];

        if (!updateDict.Is(DictWithStringKeyArg.Descriptor))
        {
            throw new Exception("Invalid update dict protobuf content.");
        }

        var dict = updateDict.Unpack<DictWithStringKeyArg>();

        foreach (var (key, value) in dict.PayLoad)
        {
            var realKey = RpcHelper.KeyCast<TK>(key);

            var valueType = UnpackValueType;

            var realValue = RpcHelper.CreateRpcPropertyContainerByType(
                valueType,
                value);

            // TODO: set rpc container
            this.IndexSetInternal(realKey, (TV)realValue.GetRawValue(), false);
        }
    }

    /// <inheritdoc/>
    void ISyncOpActionRemoveElem.Apply(RepeatedField<Any> args)
    {
        foreach (var any in args)
        {
            if (!any.Is(StringArg.Descriptor))
            {
                throw new Exception("Invalid dict remove key type");
            }

            var key = RpcHelper.GetString(any);
            var realKey = RpcHelper.KeyCast<TK>(key);
            this.RemoveInternal(realKey, false);
        }
    }

    /// <inheritdoc/>
    void ISyncOpActionClear.Apply() => this.ClearInternal(false);

    private void SetValueInternal(Dictionary<TK, RpcPropertyContainer> value, bool withNotify, bool bySync)
    {
        if (!bySync)
        {
            this.AssertNotShadowPropertyChange();
        }

        ArgumentNullException.ThrowIfNull(value);

        if (withNotify)
        {
            foreach (var (_, old) in this.RawValue)
            {
                old.RemoveFromPropTree();
            }
        }

        this.Children!.Clear();

        if (withNotify)
        {
            foreach (var (k, @new) in value)
            {
                @new.InsertToPropTree(this, $"{k}", this.TopOwner);
                this.Children[@new.Name!] = @new;
            }
        }
        else
        {
            foreach (var (k, @new) in value)
            {
                @new.Name = $"{k}";
                this.Children[@new.Name!] = @new;
            }
        }

        this.rawValue = value;

        if (withNotify)
        {
            if (this.OnSetValue != null)
            {
                var old = this.ToCopy();
                this.rawValue = value;
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.Dict);
                this.OnSetValue(old, this.ToCopy());
            }
            else
            {
                this.rawValue = value;
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.Dict);
            }
        }
        else
        {
            this.rawValue = value;
        }
    }

    private void IndexSetInternal(TK key, TV value, bool notifyChange)
    {
        ArgumentNullException.ThrowIfNull(value);
        RpcPropertyContainer? old = null;

        if (this.RawValue.ContainsKey(key))
        {
            old = this.RawValue[key];
        }

        if (value is RpcPropertyContainer container)
        {
            old?.RemoveFromPropTree();

            container.InsertToPropTree(this, $"{key}", this.TopOwner);

            this.RawValue[key] = container;
            this.Children![container.Name!] = container;

            if (notifyChange)
            {
                this.NotifyChange(
                    RpcPropertySyncOperation.UpdatePair,
                    container.Name!,
                    container,
                    RpcSyncPropertyType.Dict);
            }

            this.OnUpdatePair?.Invoke(key, old != null ? (TV)old.GetRawValue() : default, value);
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

                this.RawValue[key] = newContainer;
                this.Children![newContainer.Name] = newContainer;

                if (notifyChange)
                {
                    this.NotifyChange(
                        RpcPropertySyncOperation.UpdatePair,
                        newContainer.Name!,
                        newContainer,
                        RpcSyncPropertyType.Dict);
                }

                this.OnUpdatePair?.Invoke(key, oldVal: old != null ? (TV)old.GetRawValue() : default, value);
            }

            // reuse old
            else
            {
                var oldWithType = (RpcPropertyContainer<TV>)old;
                oldWithType.Value = value;

                if (notifyChange)
                {
                    this.NotifyChange(
                        RpcPropertySyncOperation.UpdatePair,
                        old.Name!,
                        oldWithType,
                        RpcSyncPropertyType.Dict);
                }

                this.OnUpdatePair?.Invoke(key, (TV)oldWithType.GetRawValue(), value);
            }
        }
    }

    private void RemoveInternal(TK key, bool notifyChange)
    {
        this.AssertNotShadowPropertyChange();
        var elem = this.RawValue[key];
        elem.RemoveFromPropTree();

        this.RawValue.Remove(key);
        this.Children!.Remove($"{key}");

        if (notifyChange)
        {
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name!, null, RpcSyncPropertyType.Dict);
        }

        this.OnRemoveElem?.Invoke(key, (TV)elem.GetRawValue());
    }

    private void ClearInternal(bool notifyChange)
    {
        this.AssertNotShadowPropertyChange();

        foreach (var (_, value) in this.RawValue)
        {
            value.RemoveFromPropTree();
        }

        this.RawValue.Clear();
        this.Children!.Clear();

        if (notifyChange)
        {
            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, RpcSyncPropertyType.Dict);
        }

        this.OnClear?.Invoke();
    }

    private void AssignInternal(RpcDictionary<TK, TV> target, bool notifyChange)
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

        if (notifyChange)
        {
            this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, target, RpcSyncPropertyType.List);
        }

        this.AssignInternal(target);
    }
}