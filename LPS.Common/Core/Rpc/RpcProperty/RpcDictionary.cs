using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcDictionary<TK, TV> : RpcProperty<Dictionary<TK, RpcContainerNode<TV>>>
        where TK : notnull
    {
        public Action<TK, TV>? OnElemSet { get; set; }
        public Action<TK, TV>? OnElemRemoved { get; set; }
        public Action<TK, TV>? OnNewElemAppended { get; set; }
        
        // the container of this RpcDictionary
        public RpcContainerNode Container { get; set; }

        public RpcDictionary(string name, RpcPropertySetting setting) : base(name, setting, new())
        {
        }

        public TV this[TK key]
        {
            get => value_[key].Value;
            set
            {
                if (value_.ContainsKey(key))
                {
                    value_[key].Value = value;
                }
                else
                {
                    value_[key] = new RpcContainerNode<TV>(value)
                    {
                    };
                }
            }
        }

        public void Clear() => value_.Clear();
        public bool Remove(TK key) => value_.Remove(key);
        public ReadOnlyDictionary<TK, RpcContainerNode<TV>> AsReadOnly() => new(value_);
    }

    public class ShadowRpcDictionary<TK, TV> : ShadowRpcProperty<Dictionary<TK, RpcContainerNode<TV>>>
        where TK : notnull
    {
        public ShadowRpcDictionary(string name) : base(name)
        {
            // this.GetType().GetProperty("111").SetMethod.GetMethodBody().
            Expression<Func<int>> add = () => 1 + 2;
            var func = add.Compile(); // Create Delegate
            // func.Method.GetMethodBody()
        }
        
        public TV this[TK key]
        {
            get => value_[key].Value;
            set => throw new Exception("ShadowRpcDictionary is readonly.");
        }
        
        public ReadOnlyDictionary<TK, RpcContainerNode<TV>> AsReadOnly() => new(value_);
    }
}
