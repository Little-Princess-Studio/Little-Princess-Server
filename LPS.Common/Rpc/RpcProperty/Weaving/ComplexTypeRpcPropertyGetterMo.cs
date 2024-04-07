// -----------------------------------------------------------------------
// <copyright file="ComplexTypeRpcPropertyGetterMo.cs" company="Little Princess Studio">
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
public class ComplexTypeRpcPropertyGetterMo : IMo
{
    /// <inheritdoc/>
    public AccessFlags Flags => AccessFlags.Instance | AccessFlags.PropertyGetter;

    /// <inheritdoc/>
    public string? Pattern => "getter(LPS.Common.Rpc.RpcProperty.RpcContainer.RpcPropertyContainer+ *)";

    /// <inheritdoc/>
    public Feature Features => Feature.EntryReplace;

    /// <inheritdoc/>
    public double Order => 1.0;

    /// <inheritdoc/>
    public Omit MethodContextOmits { get; } = Omit.None;

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
            var rpcProp = caller.GetGetableContainer(propName);
            context.ReplaceReturnValue(this, rpcProp.GetValue());
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