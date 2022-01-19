namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcContainerNode
    {
        public RpcContainerNode? Parent { get; set; }
        public RpcProperty? Owner { get; set; }
    }

    public class RpcContainerNode<T> : RpcContainerNode
    {
        public RpcContainerNode(T value)
        {
            Value = value;
        }

        public T Value { get; set; }

        public static implicit operator T(RpcContainerNode<T> wrapper) => wrapper.Value;

        public static implicit operator RpcContainerNode<T>(T raw) => new(raw);
    }
}