using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;

namespace LPS.Core.Rpc.RpcProperty
{
    [AttributeUsage(AttributeTargets.Field)]
    public class RpcCostumePropertyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RpcCostumePropertyContainerAttribute : Attribute
    {
    }

    public abstract class RpcPropertyContainer
    {
        public string? Name { get; set; }
        public RpcPropertyContainer? Parent { get; set; }
        public RpcProperty? Owner { get; set; }
        public bool IsReferred { get; set; }
        public Dictionary<string, RpcPropertyContainer>? Children { get; set; }
        public RpcProperty? TopOwner { get; private set; }

        public abstract object GetRawValue();
        
        public bool IsShadow => this.TopOwner?.IsShadowProperty ?? false;

        public void UpdateTopOwner(RpcProperty? topOwner)
        {
            this.TopOwner = topOwner;

            if (Children != null)
            {
                foreach (var (_, child) in this.Children)
                {
                    child.UpdateTopOwner(topOwner);
                }
            }
        }

        protected void NotifyChange(RpcPropertySyncOperation operation, string name, object? old, object? @new)
        {
            var pathList = new List<string> {name};
            this.NotifyChange(operation, pathList, old, @new);
        }

        protected void NotifyChange(RpcPropertySyncOperation operation, List<string> path, object? old, object? @new)
        {
            path.Insert(0, Name!);

            if (this.Owner != null)
            {
                this.Owner.OnNotify(operation, path, old, @new);
            }
            else
            {
                this.Parent?.NotifyChange(operation, path, old, @new);
            }
        }

        protected RpcPropertyContainer()
        {
            if (this.GetType().IsDefined(typeof(RpcCostumePropertyContainerAttribute)))
            {
                var rpcFields = this.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(field => field.IsDefined(typeof(RpcCostumePropertyAttribute))
                                    && field.FieldType.IsSubclassOf(typeof(RpcPropertyContainer)));

                // build children
                this.Children = new Dictionary<string, RpcPropertyContainer>();

                foreach (var fieldInfo in rpcFields)
                {
                    var prop = (fieldInfo.GetValue(this) as RpcPropertyContainer)!;
                    prop.Parent = this;
                    prop.IsReferred = true;
                    prop.Name = fieldInfo.Name;
                    prop.TopOwner = this.TopOwner;

                    this.Children.Add(prop.Name, prop);
                }
            }
        }
        
        // public string ToJson();
        public abstract Any ToRpcArg();
    }

    public class RpcPropertyContainer<T> : RpcPropertyContainer
    {
        static RpcPropertyContainer()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
        }

        private T value_;

        public T Value
        {
            get => value_;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (value.Equals(this.value_))
                {
                    return;
                }

                this.SetWithoutNotify(value);
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, old: value_!, value);
            }
        }

        public void SetWithoutNotify(T v)
        {
            this.value_ = v;
        }

        public static implicit operator T(RpcPropertyContainer<T> container) => container.Value;
        public static implicit operator RpcPropertyContainer<T>(T value) => new(value);

        public RpcPropertyContainer(T initVal)
        {
            value_ = initVal;
        }

        public override object GetRawValue()
        {
            return this.Value!;
        }

        public override Any ToRpcArg()
        {
            throw new NotImplementedException();
        }
    }
}