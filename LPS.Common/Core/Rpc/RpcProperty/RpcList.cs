﻿using System.Diagnostics.CodeAnalysis;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcList<TElem> : RpcPropertyContainer<List<RpcPropertyContainer<TElem>>>
    {
        public RpcList(): base(new List<RpcPropertyContainer<TElem>>())
        {
        }
        
        public RpcList(int size, [DisallowNull] TElem defaultVal): base(new List<RpcPropertyContainer<TElem>>(size))
        {
            ArgumentNullException.ThrowIfNull(defaultVal);
            for (int i = 0; i < size; i++)
            {
                var newContainer = new RpcPropertyContainer<TElem>(defaultVal)
                {
                    Parent = this,
                    Name = "${i}",
                };

                this.HandleIfContainer<TElem>(newContainer, defaultVal);
                this.Value[i] = newContainer;
            }
        }

        public void Add([DisallowNull] TElem elem)
        {
            ArgumentNullException.ThrowIfNull(elem);
            var newContainer = new RpcPropertyContainer<TElem>(elem)
            {
                Parent = this,
                Name = $"{Value.Count}",
                Reffered = true,
            };
            
            this.HandleIfContainer<TElem>(newContainer, elem);
            this.Value.Add(newContainer);
            this.NotifyChange(newContainer.Name, null, elem);
        }

        public TElem this[int index]
        {
            get => this.Value[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                this.HandleIfContainer<TElem>(this.Value[index], value);

                var old = this.Value[index].Value!;
                if (old is RpcPropertyContainer oldContainer)
                {
                    oldContainer.Reffered = false;
                }

                this.Value[index].Value = value;
                this.NotifyChange(this.Value[index].Name, old, value);
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
