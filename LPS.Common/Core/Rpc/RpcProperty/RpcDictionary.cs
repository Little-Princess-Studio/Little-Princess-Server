// using System.Collections.ObjectModel;
// using System.Linq.Expressions;
//

using System.Collections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcPropertySync;

namespace LPS.Common.Core.Rpc.RpcProperty
{
    [RpcPropertyContainer]
    public class RpcDictionary<TK, TV> : RpcPropertyContainer, IDictionary<TK, TV>
        where TK : notnull
    {
        private Dictionary<TK, RpcPropertyContainer> rawValue_;

        public OnSetValueCallBack<Dictionary<TK, TV>>? OnSetValue { get; set; }
        public OnUpdateValueCallBack<TK, TV?>? OnUpdateValue { get; set; }
        public OnRemoveElemCallBack<TK, TV>? OnRemoveElem { get; set; }
        public OnClearCallBack? OnClear { get; set; }

        public Dictionary<TK, RpcPropertyContainer> RawValue
        {
            get => rawValue_;
            private set => this.SetValueInternal(value, true, false);
        }

        private void SetValueInternal(Dictionary<TK, RpcPropertyContainer> value, bool withNotify, bool bySync)
        {
            if (!bySync)
            {
                AssertNotShadowPropertyChange();
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

            this.rawValue_ = value;

            if (withNotify)
            {
                if (this.OnSetValue != null)
                {
                    var old = this.ToCopy();
                    this.rawValue_ = value;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.Dict);
                    this.OnSetValue(old, this.ToCopy());
                }
                else
                {
                    this.rawValue_ = value;
                    this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this, RpcSyncPropertyType.Dict);
                }
            }
            else
            {
                this.rawValue_ = value;
            }
        }

        static RpcDictionary()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidKeyType<TK>();
            RpcHelper.RegisterRpcPropertyContainer(typeof(RpcDictionary<TK, TV>));
        }

        public RpcDictionary()
        {
            this.rawValue_ = new();
            this.Children = new();
        }

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

            foreach (var (k, @new) in targetContainer.rawValue_)
            {
                @new.InsertToPropTree(this, $"{k}", this.TopOwner);
                this.Children![@new.Name!] = @new;
            }
        }

        public override Any ToRpcArg()
        {
            IMessage? pbDictVal = null;

            if (this.RawValue.Count > 0)
            {
                pbDictVal = RpcHelper.RpcContainerDictToProtoBufAny(this);
            }
            else
            {
                pbDictVal = new NullArg();
            }

            return Any.Pack(pbDictVal);
        }

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer FromRpcArg(Any content)
        {
            if (content.Is(NullArg.Descriptor))
            {
                return new RpcDictionary<TK, TV>();
            }

            if (content.Is(DictWithStringKeyArg.Descriptor) && typeof(string) == typeof(TK))
            {
                var payload = content.Unpack<DictWithStringKeyArg>().PayLoad;

                RpcDictionary<string, TV> rpcDict = new();
                Dictionary<string, RpcPropertyContainer> rawDict = new();

                foreach (var (key, value) in payload)
                {
                    var val = RpcHelper.CreateRpcPropertyContainerByType(typeof(TV), value!);
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
                    var val = RpcHelper.CreateRpcPropertyContainerByType(typeof(TV), value!);
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
                    var val = RpcHelper.CreateRpcPropertyContainerByType(typeof(TV), pair.Value);
                    val.Name = $"{key}";
                    val.IsReferred = true;
                    rawDict[key] = val;
                }

                rpcDict.SetValueInternal(rawDict, true, true);
                return rpcDict;
            }

            throw new Exception($"Invalid dict content : {content}");
        }

        public TV this[TK key]
        {
            get => (TV) this.RawValue[key].GetRawValue();
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                RpcPropertyContainer? old = null;

                if (this.RawValue.ContainsKey(key))
                {
                    old = this.RawValue[key];
                }

                if (value is RpcPropertyContainer container)
                {
                    if (old != null)
                    {
                        old.RemoveFromPropTree();
                    }

                    container.InsertToPropTree(this, $"{key}", this.TopOwner);

                    this.RawValue[key] = container;
                    this.Children![container.Name!] = container;
                    this.NotifyChange(RpcPropertySyncOperation.UpdateDict, container.Name!, container,
                        RpcSyncPropertyType.Dict);
                    this.OnUpdateValue?.Invoke(key, old != null ? (TV) old.GetRawValue() : default(TV), value);
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
                        this.NotifyChange(RpcPropertySyncOperation.UpdateDict, newContainer.Name!, newContainer,
                            RpcSyncPropertyType.Dict);
                        this.OnUpdateValue?.Invoke(key, old != null ? (TV) old.GetRawValue() : default(TV), value);
                    }
                    else // reuse old
                    {
                        var oldWithType = (RpcPropertyContainer<TV>) old;
                        var oldVal = oldWithType.GetRawValue();
                        oldWithType.Value = value;
                        this.NotifyChange(RpcPropertySyncOperation.UpdateDict, old.Name!, oldWithType,
                            RpcSyncPropertyType.Dict);
                        this.OnUpdateValue?.Invoke(key, (TV) oldWithType.GetRawValue(), value);
                    }
                }
            }
        }

        public void Remove(TK key)
        {
            AssertNotShadowPropertyChange();
            var elem = this.RawValue[key];
            elem.RemoveFromPropTree();

            this.RawValue.Remove(key);
            this.Children!.Remove($"{key}");
            this.NotifyChange(RpcPropertySyncOperation.RemoveElem, elem.Name!, null, RpcSyncPropertyType.Dict);
            this.OnRemoveElem?.Invoke(key, (TV) elem.GetRawValue());
        }

        public void Clear()
        {
            AssertNotShadowPropertyChange();

            foreach (var (_, value) in this.RawValue)
            {
                value.RemoveFromPropTree();
            }

            this.RawValue.Clear();
            this.Children!.Clear();

            this.NotifyChange(RpcPropertySyncOperation.Clear, this.Name!, null, RpcSyncPropertyType.Dict);
            this.OnClear?.Invoke();
        }

        public Dictionary<TK, TV> ToCopy() =>
            this.RawValue.ToDictionary(
                pair => pair.Key,
                pair => (TV) pair.Value.GetRawValue());

        public int Count => this.RawValue.Count;

        public bool TryGetValue(TK key, out TV value)
        {
            if (this.RawValue.ContainsKey(key))
            {
                value = (TV) this.rawValue_[key].GetRawValue();
                return true;
            }

            value = default(TV);
            return false;
        }

        public ICollection<TK> Keys => this.rawValue_.Keys;
        public ICollection<TV> Values => this.rawValue_.Values.Select(v => (TV) v.GetRawValue()).ToArray();

        public void Add(TK key, TV value) => this[key] = value;

        public bool ContainsKey(TK key) => this.rawValue_.ContainsKey(key);

        bool IDictionary<TK, TV>.Remove(TK key)
        {
            if (this.ContainsKey(key))
            {
                this.Remove(key);
                return true;
            }

            return false;
        }

        public void Add(KeyValuePair<TK, TV> item) => this[item.Key] = item.Value;

        public bool Contains(KeyValuePair<TK, TV> item)
        {
            return this.ContainsKey(item.Key) && (this[item.Key]!.Equals(item.Value));
        }

        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            var keys = this.Keys.ToArray();
            for (int i = 0; i < this.Count; ++i)
            {
                array[arrayIndex++] = new KeyValuePair<TK, TV>(keys[i], this[keys[i]]);
            }
        }

        public bool Remove(KeyValuePair<TK, TV> item)
        {
            if (this.Contains(item))
            {
                this.Remove(item.Key);
                return true;
            }

            return false;
        }

        public bool IsReadOnly => false;

        public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            return new Enumerator(rawValue_);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class Enumerator : IEnumerator<KeyValuePair<TK, TV>>
        {
            private Dictionary<TK, RpcPropertyContainer>.Enumerator enumerator_;
            private Dictionary<TK, RpcPropertyContainer> dictionary_;

            public Enumerator(Dictionary<TK, RpcPropertyContainer> dictionary)
            {
                enumerator_ = dictionary.GetEnumerator();
                dictionary_ = dictionary;
            }

            public bool MoveNext() => enumerator_.MoveNext();

            public void Reset()
            {
                enumerator_.Dispose();
                enumerator_ = dictionary_.GetEnumerator();
            }

            public KeyValuePair<TK, TV> Current => new KeyValuePair<TK, TV>(enumerator_.Current.Key,
                (TV) enumerator_.Current.Value.GetRawValue());

            object IEnumerator.Current => Current;

            public void Dispose() => enumerator_.Dispose();
        }
    }
}