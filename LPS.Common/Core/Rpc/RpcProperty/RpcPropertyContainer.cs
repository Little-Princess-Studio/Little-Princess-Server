using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcPropertySync;

namespace LPS.Common.Core.Rpc.RpcProperty
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
        public bool IsReferred { get; set; }
        public Dictionary<string, RpcPropertyContainer>? Children { get; set; }
        public Common.Core.Rpc.RpcProperty.RpcProperty? TopOwner { get; private set; }

        public abstract void AssignInternal(RpcPropertyContainer target);

        public virtual object GetRawValue()
        {
            return this;
        }

        public bool IsShadow => this.TopOwner?.IsShadowProperty ?? false;

        public void RemoveFromPropTree()
        {
            this.Name = string.Empty;
            this.Parent = null;
            this.IsReferred = false;
            this.UpdateTopOwner(null);
        }

        public void InsertToPropTree(RpcPropertyContainer? parent, string name, Common.Core.Rpc.RpcProperty.RpcProperty? topOwner)
        {
            this.Name = name;
            this.Parent = parent;
            this.IsReferred = true;
            this.UpdateTopOwner(topOwner);
        }

        public void UpdateTopOwner(Common.Core.Rpc.RpcProperty.RpcProperty? topOwner)
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

        protected void NotifyChange(RpcPropertySyncOperation operation, string name, RpcPropertyContainer? @new)
        {
            var pathList = new List<string> {name};
            this.NotifyChange(operation, pathList, @new);
        }

        protected void NotifyChange(RpcPropertySyncOperation operation, List<string> path, RpcPropertyContainer? @new)
        {
            path.Insert(0, Name!);

            if (this.Parent == null)
            {
                this.TopOwner?.OnNotify(operation, path, @new);
            }
            else
            {
                this.Parent.NotifyChange(operation, path, @new);
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
                    if (!fieldInfo.IsInitOnly)
                    {
                        throw new Exception("Rpc property must be init-only.");
                    }

                    var prop = (fieldInfo.GetValue(this) as RpcPropertyContainer)!;

                    if (prop == null)
                    {
                        throw new Exception("Rpc property must be initialized with a non-null value.");
                    }

                    prop.IsReferred = true;
                    prop.Name = fieldInfo.Name;
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

            return Any.Pack(pbChildren != null ? pbChildren : new NullArg());
        }

        public void AssertNotShadowPropertyChange()
        {
            if (this.TopOwner is {IsShadowProperty: true})
            {
                throw new Exception("Shadow property cannot be modified manually");
            }
        }
    }

    public abstract class RpcPropertyCostumeContainer<TSub> : RpcPropertyContainer
        where TSub : RpcPropertyContainer, new()
    {
        public OnSetValueCallBack<TSub>? OnSetValue { get; set; }

        public void Assign(TSub target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            
            this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, target);
            this.OnSetValue?.Invoke((this as TSub)!, (target as TSub)!);

            this.AssignInternal(target);
        }
        
        public override void AssignInternal(RpcPropertyContainer target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (typeof(TSub) != target.GetType())
            {
                throw new Exception("Cannot apply assign between different types.");
            }

            target.RemoveFromPropTree();
            var props = this.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(field => field.IsDefined(typeof(RpcPropertyAttribute)));
            
            foreach (var fieldInfo in props)
            {
                var rpcPropertyOld = (fieldInfo.GetValue(this) as RpcPropertyContainer)!;
                var rpcPropertyNew = (fieldInfo.GetValue(target) as RpcPropertyContainer)!;

                if (this.Children!.ContainsKey(rpcPropertyOld.Name!))
                {
                    rpcPropertyOld.AssignInternal(rpcPropertyNew);
                }
            }
        }

        public virtual void Deserialize(Any content)
        {
            if (content.Is(DictWithStringKeyArg.Descriptor))
            {
                var dict = content.Unpack<DictWithStringKeyArg>();
                var props = this.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Where(field => field.IsDefined(typeof(RpcPropertyAttribute)));

                this.Children!.Clear();

                foreach (var fieldInfo in props)
                {
                    var rpcProperty = (fieldInfo.GetValue(this) as RpcPropertyContainer)!;

                    if (dict.PayLoad.ContainsKey(rpcProperty.Name!))
                    {
                        var fieldValue = RpcHelper.CreateRpcPropertyContainerByType(fieldInfo.FieldType,
                            dict.PayLoad[rpcProperty.Name!]);

                        fieldValue.Name = rpcProperty.Name!;
                        fieldValue.IsReferred = true;
                        fieldInfo.SetValue(this, fieldValue);
                        this.Children.Add(rpcProperty.Name!, fieldValue);
                    }
                }
            }
        }

        public static RpcPropertyContainer CreateSerializedContainer<T>(Any content)
            where T : RpcPropertyCostumeContainer<T>, new()
        {
            var obj = new T();
            obj.Deserialize(content);
            return obj;
        }
    }

    [RpcPropertyContainer]
    public class RpcPropertyContainer<T> : RpcPropertyContainer
    {
        public OnSetValueCallBack<T>? OnSetValue { get; set; }

        static RpcPropertyContainer()
        {
            RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
            RpcHelper.RegisterRpcPropertyContainer(typeof(RpcPropertyContainer<T>));
        }

        private T value_;

        public T Value
        {
            get => value_;
            set => this.Set(value, true, false);
        }

        public void Set(T value, bool withNotify, bool bySync)
        {
            if (!bySync)
            {
                AssertNotShadowPropertyChange();
            }

            ArgumentNullException.ThrowIfNull(value);
            if (value.Equals(this.value_))
            {
                return;
            }

            if (withNotify)
            {
                var old = this.value_;
                this.value_ = value;
                this.NotifyChange(RpcPropertySyncOperation.SetValue, this.Name!, this);
                this.OnSetValue?.Invoke(old, value);
            }
            else
            {
                this.value_ = value;
            }
        }

        public static implicit operator T(RpcPropertyContainer<T> container) => container.Value;
        public static implicit operator RpcPropertyContainer<T>(T value) => new(value);

        public RpcPropertyContainer(T initVal)
        {
            value_ = initVal;
        }

        public override void AssignInternal(RpcPropertyContainer target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.GetType() != typeof(RpcPropertyContainer<T>))
            {
                throw new Exception("Cannot apply assign between different types.");
            }

            var targetContainer = (target as RpcPropertyContainer<T>)!;
            this.value_ = targetContainer.value_;
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