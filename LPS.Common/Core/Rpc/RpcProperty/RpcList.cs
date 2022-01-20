using System.Diagnostics.CodeAnalysis;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcList<TElem> : RpcPropertyContainer<List<RpcPropertyContainer<TElem>>>
    {
        public RpcList(): base(new List<RpcPropertyContainer<TElem>>())
        {
        }
        
        public RpcList(int size, TElem defaultVal): base(new List<RpcPropertyContainer<TElem>>(size))
        {
            for (int i = 0; i < size; i++)
            {
                this.Value[i] = new RpcPropertyContainer<TElem>(defaultVal)
                {
                    Parent = this,
                    Name = "${i}",
                };
            }
        }

        public void Add([DisallowNull] TElem elem)
        {
            ArgumentNullException.ThrowIfNull(elem);
            var newElem = new RpcPropertyContainer<TElem>(elem)
            {
                Parent = this,
                Name = $"{Value.Count}",
                Reffered = true,
            };
            
            if (elem is RpcPropertyContainer container)
            {
                if (container.Reffered)
                {
                    throw new Exception("Each object in rpc property can only be reffered once");
                }

                container.Parent = newElem;
                container.IsProxyContainer = true;
                container.Reffered = true;
            }

            this.Value.Add(newElem);
            var pathList = new List<string>() { newElem.Name };
            this.NotifyChange(pathList, null, elem);
        }

        public TElem this[int index]
        {
            get => this.Value[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var pathList = new List<string> { this.Value[index].Name };
                
                if (value is RpcPropertyContainer container)
                {
                    if (container.Reffered)
                    {
                        throw new Exception("Each object in rpc property can only be reffered once");
                    }

                    container.Parent = this.Value[index];
                    container.IsProxyContainer = true;
                    container.Reffered = true;
                }

                var old = this.Value[index].Value!;
                if (old is RpcPropertyContainer oldContainer)
                {
                    oldContainer.Reffered = false;
                }

                this.Value[index].Value = value;
                this.NotifyChange(pathList, old, value);
            }
        }
    }

    // public class ShadowRpcList<TElem> : List<RpcContainerNode<TElem>>
    // {
    //     public ShadowRpcList()
    //     {
    //     }
    // }
}
