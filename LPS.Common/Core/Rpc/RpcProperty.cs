using LPS.Core.Entity;

namespace LPS.Core.Rpc
{
    public enum RpcPropertySetting
    {
        None = 0x00000000,
        Permanent = 0x00000001,
        ServerOwn = 0x00000010,
        ClientOwn = 0x00000100,
        FastSync = 0x00001000,
    }

    public abstract class RpcProperty
    {
        public readonly string Name;
        public RpcProperty? Parent { get; set; }
        public BaseEntity? owner_;
        public BaseEntity? Owner
        {
            get
            {
                if (this.owner_ != null)
                {
                    return this.owner_;
                }

                var p = this.Parent;
                while (p != null)
                {
                    p = p.Parent;
                }

                return p?.owner_;
            }
        }

        public RpcProperty(string name)
        {
            Name = name;
        }
    }
    
    public class RpcProperty<T> : RpcProperty
    {
        public readonly RpcPropertySetting Setting;
        protected T value_;

        public Action<T, T>? OnChanged { get; set; }

        public RpcProperty(string name, RpcPropertySetting setting, T value): base(name)
        {
            this.Setting = setting;
            this.value_ = value;
        }

        public void Set(T value)
        {
            this.OnChanged?.Invoke(this.value_, value);
            this.value_ = value;
        }
    }

    public class RpcDictionary<TK, TV> : RpcProperty<Dictionary<TK, TV>>
        where TK : notnull
        where TV: RpcProperty
    {
        public Action<TK, TV>? OnElemSet { get; set; }
        public Action<TK, TV>? OnElemRemoved { get; set; }
        public Action<TK, TV>? OnNewElemAppended { get; set; }

        public RpcDictionary(string name, RpcPropertySetting setting) : base(name, setting, new ())
        {
        }

        public TV this[TK key]
        {
            get => value_[key];
            set
            {
                value.Parent = this;
                value_[key] = value;
            }
        }

        public void Clear() => value_.Clear();
        public bool Remove(TK key) => value_.Remove(key);
    }

    public class RpcList<TElem> : RpcProperty<List<TElem>> where TElem: RpcProperty
    {
        public RpcList(string name, RpcPropertySetting setting, List<TElem> value) : base(name, setting, new())
        {
        }

        public TElem this[int index]
        {
            get => value_[index];
            set
            {
                value_[index] = value;
                value.Parent = this;
            }
        }
    }
}
