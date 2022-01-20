// using System.Collections.ObjectModel;
// using System.Linq.Expressions;
//

using System.Diagnostics.CodeAnalysis;

namespace LPS.Core.Rpc.RpcProperty
{
    public class RpcDictionary<TK, TV> : RpcPropertyContainer<Dictionary<TK, RpcPropertyContainer<TV>>>
        where TK : notnull
    {
        public RpcDictionary() : base(new())
        {
        }

        public TV this[TK key]
        {
            get => this.Value[key].Value;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (this.Value.ContainsKey(key))
                {
                    this.HandleIfContainer(this.Value[key], value);

                    var old = this.Value[key].Value;
                    if (old is RpcPropertyContainer container)
                    {
                        container.Reffered = false;
                    }

                    this.Value[key].Value = value;
                    this.NotifyChange(this.Value[key].Name, old!, value);
                }
                else
                {
                    var newContainer = new RpcPropertyContainer<TV>(value)
                    {
                        Parent = this,
                        Name = $"{key}",
                        Reffered = true,
                    };
                    
                    this.HandleIfContainer<TV>(newContainer, value);
                    this.Value[key] = newContainer;
                    this.NotifyChange(this.Value[key].Name, null, value);
                }
            }
        }
        
        // public void Clear() => this.Value.Clear();
        // public bool Remove(TK key) => this.Value.Remove(key);
    }
}

//
//     public class ShadowRpcDictionary<TK, TV> : ShadowRpcProperty<Dictionary<TK, RpcContainerNode<TV>>>
//         where TK : notnull
//     {
//         public ShadowRpcDictionary(string name) : base(name)
//         {
//             // this.GetType().GetProperty("111").SetMethod.GetMethodBody().
//             Expression<Func<int>> add = () => 1 + 2;
//             var func = add.Compile(); // Create Delegate
//             // func.Method.GetMethodBody()
//         }
//         
//         public TV this[TK key]
//         {
//             get => value_[key].Value;
//             set => throw new Exception("ShadowRpcDictionary is readonly.");
//         }
//         
//         public ReadOnlyDictionary<TK, RpcContainerNode<TV>> AsReadOnly() => new(value_);
//     }
// }
