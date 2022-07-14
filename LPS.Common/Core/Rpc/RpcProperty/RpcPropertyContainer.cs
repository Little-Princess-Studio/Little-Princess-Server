using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

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

        public virtual object GetRawValue()
        {
            return this;
        }
        
        public bool IsShadow => this.TopOwner?.IsShadowProperty ?? false;

        public void RemoveFromPropTree()
        {
            this.Name = string.Empty;
            this.Parent = null;
            this.Owner = null;
            this.IsReferred = false;
            this.UpdateTopOwner(null);
        }

        public void InsertToPropTree(RpcPropertyContainer? parent, string name, RpcProperty? topOwner)
        {
            this.Name = name;
            this.Parent = parent;
            this.IsReferred = true;
            this.UpdateTopOwner(topOwner);
        }
        
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
        
        public virtual Any ToRpcArg()
        {
            DictWithStringKeyArg? pbChildren = null;

            if (this.Children!.Count > 0)
            {
                pbChildren = new DictWithStringKeyArg();

                foreach (var (name, value) in this.Children)
                {
                    pbChildren.PayLoad.Add(name, value.ToRpcArg());
                }
            }

            var pbRpc = new DictWithStringKeyArg();
            pbRpc.PayLoad.Add("children", pbChildren == null ? Any.Pack(new NullArg()) : Any.Pack(pbChildren));

            return Any.Pack(pbRpc);
        }

        public virtual void FromRpcArg(Any content)
        {
            // todo: normal sync
        }
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
            return Any.Pack(RpcHelper.RpcArgToProtobuf(this.value_));
        }
        
        public override void FromRpcArg(Any content)
        {
            try
            {
                var value = RpcHelper.ProtobufToRpcArg(content, typeof(T));
                this.value_ = (T) value!;
            }
            catch (Exception e)
            {
                Debug.Logger.Warn(e, "Error when sync prop");
            }
        }
    }
}