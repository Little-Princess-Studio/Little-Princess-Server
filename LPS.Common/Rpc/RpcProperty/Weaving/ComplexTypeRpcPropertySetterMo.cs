// -----------------------------------------------------------------------
// <copyright file="ComplexTypeRpcPropertySetterMo.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty.Weaving;

using Rougamo;
using Rougamo.Context;

/// <summary>
/// Represents a method object that implements the <see cref="IMo"/> interface and provides
/// interception for plain type RPC property getters.
/// </summary>
public class ComplexTypeRpcPropertySetterMo : IMo
{
    /// <inheritdoc/>
    public AccessFlags Flags => AccessFlags.Instance | AccessFlags.PropertySetter;

    /// <inheritdoc/>
    public string? Pattern => "setter(LPS.Common.Rpc.RpcProperty.RpcContainer.RpcPropertyContainer+ *)";

    /// <inheritdoc/>
    public Feature Features => Feature.EntryReplace;

    /// <inheritdoc/>
    public double Order => 1.0;

    /// <inheritdoc/>
    public void OnEntry(MethodContext context)
    {
        var caller = (context.Target as IPropertyTree)!;
        var propName = context.Method.Name[4..];
        var isRpcProperty = context.Target
            .GetType()
            .GetProperty(propName)?
            .IsDefined(typeof(RpcWrappedPropertyAttribute), false) ?? false;

        if (isRpcProperty && caller.IsPropertyTreeBuilt)
        {
            var rpcProp = caller.GetSetableContainer(propName);
            var newValue = context.Arguments[0];
            rpcProp.SetValue(newValue);
            context.ReplaceReturnValue(this, context.Arguments[0]);
        }
    }

    /// <inheritdoc/>
    public void OnException(MethodContext context)
    {
    }

    /// <inheritdoc/>
    public void OnExit(MethodContext context)
    {
    }

    /// <inheritdoc/>
    public void OnSuccess(MethodContext context)
    {
    }
}