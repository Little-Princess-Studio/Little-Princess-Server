using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Core.Ipc.SyncMessage;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc.RpcProperty
{
    [AttributeUsage(AttributeTargets.Field)]
    public class RpcPropertyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RpcPropertyContainerAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RpcPropertyContainerDeserializeEntryAttribute : Attribute
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
            if (this.GetType().IsDefined(typeof(RpcPropertyContainerAttribute)))
            {
                var rpcFields = this.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(field => field.IsDefined(typeof(RpcPropertyAttribute))
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
    }

    [RpcPropertyContainer]
    public class RpcPropertyContainer<T> : RpcPropertyContainer
    {
        static RpcPropertyContainer()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
            RpcHelper.RegisterRpcPropertyContainer(typeof(RpcPropertyContainer<T>));
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

        [RpcPropertyContainerDeserializeEntry]
        public static RpcPropertyContainer FromRpcArg(Any content)
        {
            RpcPropertyContainer? container = null;

            if (content.Is(IntArg.Descriptor) && typeof(T) == typeof(int))
            {
                container = new RpcPropertyContainer<int>(content.Unpack<IntArg>().PayLoad);
            }

            if (content.Is(FloatArg.Descriptor) && typeof(T) == typeof(float))
            {
                container = new RpcPropertyContainer<float>(content.Unpack<FloatArg>().PayLoad);
            }

            if (content.Is(StringArg.Descriptor) && typeof(T) == typeof(string))
            {
                container = new RpcPropertyContainer<string>(content.Unpack<StringArg>().PayLoad);
            }

            if (content.Is(BoolArg.Descriptor) && typeof(T) == typeof(bool))
            {
                container = new RpcPropertyContainer<bool>(content.Unpack<BoolArg>().PayLoad);
            }

            if (content.Is(MailBoxArg.Descriptor) && typeof(T) == typeof(MailBox))
            {
                container = new RpcPropertyContainer<MailBox>(
                    RpcHelper.PbMailBoxToRpcMailBox(content.Unpack<MailBoxArg>().PayLoad));
            }

            if (container == null)
            {
                throw new Exception($"Invalid deserialize content {content}");
            }

            return container;
        }
    }
}