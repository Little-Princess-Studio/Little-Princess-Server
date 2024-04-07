// -----------------------------------------------------------------------
// <copyright file="RpcPropertyCostumeContainer.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty.RpcContainer;

using System.Reflection;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty.Weaving;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;
using Rougamo;

#pragma warning disable SA1629

/// <summary>
/// Base Rpc property class for implementing costume container.
/// </summary>
public abstract class RpcPropertyCostumeContainer : RpcPropertyContainer,
    IRougamo<ComplexTypeRpcPropertyGetterMo>,
    IRougamo<ComplexTypeRpcPropertySetterMo>,
    IRougamo<PlaintTypeRpcPropertyGetterMo>,
    IRougamo<PlaintTypeRpcPropertySetterMo>,
    IPropertyTree
{
    /// <summary>
    /// Create a container with protobuf object.
    /// </summary>
    /// <param name="content">Protobuf object.</param>
    /// <typeparam name="T">Typeof the container.</typeparam>
    /// <returns>Rpc container.</returns>
    public static RpcPropertyContainer CreateSerializedContainer<T>(Any content)
        where T : RpcPropertyCostumeContainer<T>, new()
    {
        var obj = new T();
        obj.Deserialize(content);
        return obj;
    }

    /// <inheritdoc/>
    bool IPropertyTree.IsPropertyTreeBuilt => this.Children != null;

    /// <inheritdoc/>
    IValueGetable IPropertyTree.GetGetableContainer(string name)
    {
        if (this.Children!.ContainsKey(name))
        {
            return (IValueGetable)this.Children[name];
        }

        throw new Exception($"Property {name} not found in entity {this.GetType().Name}.");
    }

    /// <inheritdoc/>
    IValueSetable IPropertyTree.GetSetableContainer(string name)
    {
        if (this.Children!.ContainsKey(name))
        {
            return (IValueSetable)this.Children[name];
        }

        throw new Exception($"Property {name} not found in entity {this.GetType().Name}.");
    }

    /// <summary>
    /// Deserialize content from protobuf any object.
    /// </summary>
    /// <param name="content">Protobuf any object.</param>
    public void Deserialize(Any content)
    {
        if (content.Is(DictWithStringKeyArg.Descriptor))
        {
            var newChildren = new Dictionary<string, RpcPropertyContainer>();

            var dict = content.Unpack<DictWithStringKeyArg>();
            var fields = this.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(field => field.IsDefined(typeof(RpcPropertyAttribute)));

            foreach (var fieldInfo in fields)
            {
                var attr = fieldInfo.GetCustomAttribute<RpcPropertyAttribute>()!;
                var propName = string.IsNullOrEmpty(attr.Name) ? fieldInfo.Name : attr.Name;
                if (!this.Children!.TryGetValue(propName, out var rpcProperty))
                {
                    continue;
                }

                if (dict.PayLoad.ContainsKey(rpcProperty.Name!))
                {
                    var fieldValue = RpcHelper.CreateRpcPropertyContainerByType(
                        fieldInfo.FieldType,
                        dict.PayLoad[rpcProperty.Name!]);

                    fieldValue.Name = rpcProperty.Name!;
                    fieldValue.IsReferred = true;
                    fieldInfo.SetValue(this, fieldValue);
                    newChildren.Add(rpcProperty.Name!, fieldValue);
                }
            }

            var props = this.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(prop => prop.IsDefined(typeof(RpcWrappedPropertyAttribute)));

            foreach (var propInfo in props)
            {
                var attr = propInfo.GetCustomAttribute<RpcWrappedPropertyAttribute>()!;

                var propName = string.IsNullOrEmpty(attr.Name) ? propInfo.Name : attr.Name;
                if (!this.Children!.TryGetValue(propName, out var rpcProperty))
                {
                    continue;
                }

                if (dict.PayLoad.ContainsKey(rpcProperty.Name!))
                {
                    var makeGenericType = typeof(RpcPropertyContainer<>).MakeGenericType(propInfo.PropertyType);
                    var fieldValue = RpcHelper.CreateRpcPropertyContainerByType(
                        makeGenericType,
                        dict.PayLoad[rpcProperty.Name!]);

                    fieldValue.Name = rpcProperty.Name!;
                    fieldValue.IsReferred = true;
                    propInfo.SetValue(this, fieldValue.GetRawValue());
                    newChildren.Add(rpcProperty.Name!, fieldValue);
                }
            }

            this.Children = newChildren;
        }
    }
}

/// <summary>
/// Bse Rpc property class for implementing costume container.
/// And the MyContainer must have a method attributed as <see cref="RpcPropertyContainerDeserializeEntryAttribute"/>
///
/// Used as:<para />
/// class MyCostumeRpcProperty : RpcPropertyCostumeContainer&lt;MyCostumeRpcProperty&gt;<para />
/// {<para />
///     [RpcPropertyContainerDeserializeEntry]<para />
///     public static RpcPropertyContainer FromRpcArg(Any content) {}<para />
/// }<para />
/// </summary>
/// <typeparam name="TSub">Sub class type of the container's value.</typeparam>
#pragma warning restore SA1629

#pragma warning disable SA1402
public abstract class RpcPropertyCostumeContainer<TSub> : RpcPropertyCostumeContainer, ISyncOpActionSetValue
    where TSub : RpcPropertyContainer, new()
{
    /// <summary>
    /// Gets or sets the callback when setting value.
    /// </summary>
    public OnSetValueCallBack<TSub>? OnSetValue { get; set; }

    /// <summary>
    /// Create a container with protobuf object.
    /// </summary>
    /// <param name="content">Protobuf object.</param>
    /// <param name="rawType">Typeof the container.</param>
    /// <returns>Rpc container.</returns>
    public static RpcPropertyContainer CreateSerializedContainer(Any content, System.Type rawType)
    {
        var parent = typeof(RpcPropertyCostumeContainer<>).MakeGenericType(rawType);
        if (!rawType.IsSubclassOf(parent))
        {
            throw new Exception($"Type {rawType} is not a subclass of RpcPropertyCostumeContainer<T>.");
        }

        var obj = Activator.CreateInstance(rawType) as RpcPropertyCostumeContainer;
        obj!.Deserialize(content);
        return obj;
    }

    /// <summary>
    /// Assign the value of the container.
    /// </summary>
    /// <param name="target">Value.</param>
    /// <exception cref="ArgumentNullException">ArgumentNullException.</exception>
    public void Assign(TSub target) => this.AssignInternal(target, true);

    /// <inheritdoc/>
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
        var fields = this.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.IsDefined(typeof(RpcPropertyAttribute)));

        foreach (var fieldInfo in fields)
        {
            var rpcPropertyOld = (fieldInfo.GetValue(this) as RpcPropertyContainer)!;
            var rpcPropertyNew = (fieldInfo.GetValue(target) as RpcPropertyContainer)!;

            if (this.Children!.ContainsKey(rpcPropertyOld.Name!))
            {
                rpcPropertyOld.AssignInternal(rpcPropertyNew);
            }
        }

        var props = this.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(prop => prop.IsDefined(typeof(RpcWrappedPropertyAttribute)));

        foreach (PropertyInfo propInfo in props)
        {
            var rpcPropertyOld = (propInfo.GetValue(this) as RpcPropertyContainer)!;
            var rpcPropertyNew = (propInfo.GetValue(target) as RpcPropertyContainer)!;

            if (this.Children!.ContainsKey(rpcPropertyOld.Name!))
            {
                rpcPropertyOld.AssignInternal(rpcPropertyNew);
            }
        }
    }

    /// <inheritdoc/>
    void ISyncOpActionSetValue.Apply(RepeatedField<Any> args)
    {
        var value = RpcHelper.CreateRpcPropertyContainerByType(this.GetType(), args[0]);
        this.AssignInternal((TSub)value, false);
    }

    private void AssignInternal(TSub target, bool notifyChange)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (notifyChange)
        {
            this.NotifyChange(
                RpcPropertySyncOperation.SetValue,
                this.Name!,
                target,
                RpcSyncPropertyType.PlaintAndCostume);
        }

        this.OnSetValue?.Invoke((this as TSub)!, (target as TSub)!);

        this.AssignInternal(target);
    }
}
#pragma warning restore SA1402