// -----------------------------------------------------------------------
// <copyright file="RpcStubForServerClientEntityGenerator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc;

using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Entity;

/// <summary>
/// Provides methods for generating server-side implementations of RPC interfaces marked with RpcServerStubAttribute.
/// </summary>
public class RpcStubForServerClientEntityGenerator : RpcStubGenerator
{
    /// <inheritdoc/>
    protected override Type EntityType => typeof(ServerClientEntity);

    /// <inheritdoc/>
    protected override Type AttributeType => typeof(RpcClientStubAttribute);

    /// <inheritdoc/>
    protected override void ImplementRpcCallWithResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task<T>/ValueTask<T>
        var returnType = method.ReturnType.GenericTypeArguments[0];
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ServerClientEntity.ClientProxy)
            .GetMethod("Call")!
            .MakeGenericMethod(returnType);

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <inheritdoc/>
    protected override void ImplementRpcCallWithoutResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task/ValueTask
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ServerClientEntity.ClientProxy)
            .GetMethod("Call")!;

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <inheritdoc/>
    protected override void ImplementRpcCallWithNotifyOnly(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        var attr = method.GetCustomAttribute<RpcStubNotifyOnlyAttribute>();
        var methodName = string.IsNullOrEmpty(attr?.RpcMethodName) ? attr!.RpcMethodName : method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ServerClientEntity.ClientProxy)
            .GetMethod("Notify")!;

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <inheritdoc/>
    protected override void GenerateRpcCall(FieldBuilder entityField, ILGenerator ilgenerator, string methodName, Type[] parameterTypes, MethodInfo callMethod)
    {
        // var [proxy] = this.entity.Server
        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Ldfld, entityField);
        ilgenerator.Emit(
            OpCodes.Callvirt,
            this.EntityType.GetProperty("Client")!.GetGetMethod()!);

        // return proxy.Call<T>(methodName, arg0, arg1, arg2, ...);
        ilgenerator.Emit(OpCodes.Ldstr, methodName);
        for (int i = 0; i < parameterTypes.Length; i++)
        {
            ilgenerator.Emit(OpCodes.Ldarg, i + 1);
        }

        ilgenerator.Emit(OpCodes.Callvirt, callMethod);
        ilgenerator.Emit(OpCodes.Ret);
    }
}