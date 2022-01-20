namespace LPS.Core.Rpc.RpcProperty
{
    public abstract class RpcPropertyContainer
    {
        public string Name { get; set; }
        public RpcPropertyContainer? Parent { get; set; }
        public RpcProperty? Owner { get; set; }
        public bool IsProxyContainer { get; set; }
        public bool Reffered { get; set; }

        protected void NotifyChange(List<string> path, object old, object @new)
        {
            if (!IsProxyContainer)
            {
                path.Insert(0, Name);
            }
            if (this.Owner != null)
            {
                this.Owner.OnChange(path, old, @new);
            }
            else
            {
                this.Parent?.NotifyChange(path, old, @new);
            }
        }
    }

    public class RpcPropertyContainer<T> : RpcPropertyContainer
    {
        private T value_;
        public T Value
        {
            get => value_;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var pathList = new List<string>();
                this.NotifyChange(pathList, this.value_!, value!);
                this.value_ = value;
            }
        }

        public static implicit operator T(RpcPropertyContainer<T> container) => container.Value;
        // public static implicit operator RpcPropertyContainer<T>(T value) => new (value);

        public RpcPropertyContainer(T initVal)
        {
            this.value_ = initVal;
        }
    }
}
