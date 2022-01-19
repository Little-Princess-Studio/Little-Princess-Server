using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcList<TElem> : RpcProperty<List<RpcContainerNode<TElem>>>
    {
        public RpcList(string name, RpcPropertySetting setting, List<TElem> value) : base(name, setting, new())
        {
        }

        public void Clear()
        {
            value_.Clear();
        }

        public void RemoveAt(int index) => value_.RemoveAt(index);

        public void Resize(int size, [DisallowNull] TElem defaultValue)
        {
            ArgumentNullException.ThrowIfNull(defaultValue);

            if (value_.Count > 0)
            {
                throw new Exception("Resize can only used on empty RpcList");
            }

            for (int i = 0; i < size; ++i)
            {
                value_.Add(new RpcContainerNode<TElem>(defaultValue));
            }
        }

        public void Add([DisallowNull] TElem value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value_.Add(new RpcContainerNode<TElem>(value));
        }

        public TElem this[int index]
        {
            get => value_[index].Value;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                value_[index].Value = value;
            }
        }
        
        public ReadOnlyCollection<RpcContainerNode<TElem>> AsReadOnly() => value_.AsReadOnly();
    }

    public class ShadowRpcList<TElem> : ShadowRpcProperty<List<RpcContainerNode<TElem>>>
    {
        public ShadowRpcList(string name) : base(name)
        {
        }

        public TElem this[int index]
        {
            get => value_[index].Value;
            set => throw new Exception("RpcList is readonly");
        }

        public ReadOnlyCollection<RpcContainerNode<TElem>> AsReadOnly() => value_.AsReadOnly();
    }
}