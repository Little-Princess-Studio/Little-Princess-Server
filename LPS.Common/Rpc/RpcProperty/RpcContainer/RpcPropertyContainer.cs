// -----------------------------------------------------------------------
// <copyright file="RpcPropertyContainer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty.RpcContainer;

using System.Reflection;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using LPS.Common.Util;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Base class of RPC property container.
/// A container can be seemed as a node in the property tree.
/// </summary>
public abstract class RpcPropertyContainer
{
    /// <summary>
    /// Gets or sets the name of the container.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the parent of the container.
    /// </summary>
    public RpcPropertyContainer? Parent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this container is referred by a property tree.
    /// </summary>
    public bool IsReferred { get; set; }

    /// <summary>
    /// Gets or sets the children of the container.
    /// </summary>
    public Dictionary<string, RpcPropertyContainer>? Children { get; set; }

    /// <summary>
    /// Gets the top property owner of the container.
    /// </summary>
    public RpcProperty? TopOwner { get; private set; }

    /// <summary>
    /// Internal assign method.
    /// </summary>
    /// <param name="target">Target RPC property container assigned with.</param>
    public abstract void AssignInternal(RpcPropertyContainer target);

    /// <summary>
    /// Got the raw value inside the container.
    /// </summary>
    /// <returns>Raw value inside the container.</returns>
    public virtual object GetRawValue()
    {
        return this;
    }

    /// <summary>
    /// Gets a value indicating whether this container is shadow container.
    /// </summary>
    public bool IsShadow => this.TopOwner?.IsShadowProperty ?? false;

    /// <summary>
    /// Remove this container from the property tree which the container belongs to.
    /// </summary>
    public void RemoveFromPropTree()
    {
        this.Name = string.Empty;
        this.Parent = null;
        this.IsReferred = false;
        this.UpdateTopOwner(null);
    }

    /// <summary>
    /// Insert this container into a property tree.
    /// </summary>
    /// <param name="parent">Parent node of this container in the property tree.</param>
    /// <param name="name">Name of the container in the property tree.</param>
    /// <param name="topOwner">Top owner of the property tree.</param>
    public void InsertToPropTree(RpcPropertyContainer? parent, string name, RpcProperty? topOwner)
    {
        this.Name = name;
        this.Parent = parent;
        this.IsReferred = true;
        this.UpdateTopOwner(topOwner);
    }

    /// <summary>
    /// Update the top owner of the container and its children.
    /// </summary>
    /// <param name="topOwner">New top owner.</param>
    public void UpdateTopOwner(RpcProperty? topOwner)
    {
        this.TopOwner = topOwner;

        if (this.Children != null)
        {
            foreach (var (_, child) in this.Children)
            {
                child.UpdateTopOwner(topOwner);
            }
        }
    }

    /// <summary>
    /// Serialize the prop tree to protobuf Any object with this node being the root.
    /// </summary>
    /// <returns>Serialized protobuf Any object.</returns>
    public virtual Any ToRpcArg()
    {
        DictWithStringKeyArg? protobufChildren = null;

        if (this.Children!.Count > 0)
        {
            protobufChildren = new DictWithStringKeyArg();

            foreach (var (name, value) in this.Children)
            {
                protobufChildren.PayLoad.Add(name, value.ToRpcArg());
            }
        }

        return Any.Pack(protobufChildren != null ? protobufChildren : new NullArg());
    }

    /// <summary>
    /// Assert whether the top owner of the property is shadow property.
    /// </summary>
    /// <exception cref="Exception">Warning Exception.</exception>
    protected void AssertNotShadowPropertyChange()
    {
        if (this.TopOwner is { IsShadowProperty: true })
        {
            throw new Exception("Shadow property cannot be modified manually");
        }
    }

    /// <summary>
    /// Notify sync change to top owner.
    /// </summary>
    /// <param name="operation">Change operation.</param>
    /// <param name="name">Name of the container.</param>
    /// <param name="new">New value of the change (if needed).</param>
    /// <param name="propertyType">Sync property type of the change.</param>
    protected void NotifyChange(
        RpcPropertySyncOperation operation,
        string name,
        RpcPropertyContainer? @new,
        RpcSyncPropertyType propertyType)
    {
        if (this.TopOwner == null || this.TopOwner.SendSyncMsgImpl != null || !this.TopOwner.IsShadowProperty)
        {
            return;
        }

        var pathList = new List<string> { name };
        this.NotifyChange(operation, pathList, @new, propertyType);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPropertyContainer"/> class.
    /// </summary>
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

                var prop = (fieldInfo.GetValue(this) as RpcPropertyContainer)!
                    ?? throw new Exception("Rpc property must be initialized with a non-null value.");
                prop.IsReferred = true;
                prop.Name = fieldInfo.Name;
                this.Children.Add(prop.Name, prop);
            }
        }
    }

    private void NotifyChange(
        RpcPropertySyncOperation operation,
        List<string> path,
        RpcPropertyContainer? @new,
        RpcSyncPropertyType propertyType)
    {
        path.Insert(0, this.Name!);

        if (this.Parent == null)
        {
            this.TopOwner?.OnNotify(operation, path, @new, propertyType);
        }
        else
        {
            this.Parent.NotifyChange(operation, path, @new, propertyType);
        }
    }
}

/// <summary>
/// RpcPropertyContainer class for plaint RpcPropertyContainer value.
/// </summary>
/// <typeparam name="T">Type of the raw value.</typeparam>
[RpcPropertyContainer]
#pragma warning disable SA1402
public class RpcPropertyContainer<T> : RpcPropertyContainer, ISyncOpActionSetValue
#pragma warning restore SA1402
{
    /// <summary>
    /// Gets or sets the costume handler when setting value.
    /// </summary>
    public OnSetValueCallBack<T>? OnSetValue { get; set; }

    public static implicit operator T(RpcPropertyContainer<T> container) => container.Value;

    public static implicit operator RpcPropertyContainer<T>(T value) => new(value);

    /// <summary>
    /// Entry method for RpcPropertyContainer costume deserialize.
    /// </summary>
    /// <param name="content">Protobuf Any object needed be deserialized to this container.</param>
    /// <returns>Deserialized RpcPropertyContainer object.</returns>
    /// <exception cref="Exception">Throw exception if failed to deserialize.</exception>
    [RpcPropertyContainerDeserializeEntry]
    public static RpcPropertyContainer FromRpcArg(Any content)
    {
        RpcPropertyContainer? container = null;

        if (content.Is(IntArg.Descriptor) && typeof(T) == typeof(int))
        {
            container = new RpcPropertyContainer<int>(RpcHelper.GetInt(content));
        }

        if (content.Is(FloatArg.Descriptor) && typeof(T) == typeof(float))
        {
            container = new RpcPropertyContainer<float>(RpcHelper.GetFloat(content));
        }

        if (content.Is(StringArg.Descriptor) && typeof(T) == typeof(string))
        {
            container = new RpcPropertyContainer<string>(RpcHelper.GetString(content));
        }

        if (content.Is(BoolArg.Descriptor) && typeof(T) == typeof(bool))
        {
            container = new RpcPropertyContainer<bool>(RpcHelper.GetBool(content));
        }

        if (content.Is(MailBoxArg.Descriptor) && typeof(T) == typeof(MailBox))
        {
            container = new RpcPropertyContainer<MailBox>(
                RpcHelper.PbMailBoxToRpcMailBox(RpcHelper.GetMailBox(content)));
        }

        if (container == null)
        {
            throw new Exception($"Invalid deserialize content {content}");
        }

        return container;
    }

    static RpcPropertyContainer()
    {
        RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
        RpcHelper.RegisterRpcPropertyContainer(typeof(RpcPropertyContainer<T>));
    }

    private T value;

    /// <summary>
    /// Gets or sets the raw value of the container.
    /// </summary>
    public T Value
    {
        get => this.value;
        set => this.Set(value, true, false);
    }

    /// <summary>
    /// Set the raw value of the container.
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <param name="withNotify">If the setting operation should notify property tree change.</param>
    /// <param name="bySync">If this setting operation is invoked by property sync process.</param>
    public void Set(T value, bool withNotify, bool bySync)
    {
        if (!bySync)
        {
            // If the setting operation is not by sync, only non-shadow property could do this operation.
            this.AssertNotShadowPropertyChange();
        }

        ArgumentNullException.ThrowIfNull(value);
        if (value.Equals(this.value))
        {
            return;
        }

        var old = this.value;
        if (withNotify)
        {
            this.value = value;
            this.NotifyChange(
                RpcPropertySyncOperation.SetValue,
                this.Name!,
                this,
                RpcSyncPropertyType.PlaintAndCostume);
        }
        else
        {
            this.value = value;
        }

        this.OnSetValue?.Invoke(old, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPropertyContainer{T}"/> class.
    /// </summary>
    /// <param name="initVal">Initial value.</param>
    public RpcPropertyContainer(T initVal)
    {
        this.value = initVal;
    }

    /// <summary>
    /// Assign this container's value with another container's value.
    /// </summary>
    /// <param name="container">Other container.</param>
    public void Assign(RpcPropertyContainer container)
    {
        this.Set((container as RpcPropertyContainer<T>)!.value, false, true);
    }

    /// <inheritdoc/>
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
        this.value = targetContainer.value;
    }

    /// <inheritdoc/>
    public override object GetRawValue()
    {
        return this.Value!;
    }

    /// <inheritdoc/>
    public override Any ToRpcArg()
    {
        return Any.Pack(RpcHelper.RpcArgToProtoBuf(this.value));
    }

    /// <inheritdoc/>
    void ISyncOpActionSetValue.Apply(RepeatedField<Any> args)
    {
        var value = RpcHelper.CreateRpcPropertyContainerByType(typeof(RpcPropertyContainer<T>), args[0]);
        this.Set((value as RpcPropertyContainer<T>)!.value, false, true);
    }
}