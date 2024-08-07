// -----------------------------------------------------------------------
// <copyright file="RpcStubForShadowClientEntityGenerator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc;

using System.Reflection;
using System.Reflection.Emit;
using LPS.Client.Entity;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Provides methods for generating server-side implementations of RPC interfaces marked with RpcServerStubAttribute.
/// </summary>
public class RpcStubForShadowClientEntityGenerator : RpcStubGenerator
{
    /// <inheritdoc/>
    protected override Type EntityType => typeof(ShadowClientEntity);

    /// <inheritdoc/>
    protected override void ImplementRpcCallWithResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task<T>/ValueTask<T>
        var returnType = method.ReturnType.GenericTypeArguments[0];
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ShadowClientEntity.ServerProxy)
            .GetMethods()
            .First(method => method.IsGenericMethod && method.Name.Split("`")[0] == "Call")!;

        var generated = callMethod.MakeGenericMethod(returnType);
        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, generated);
    }

    /// <inheritdoc/>
    protected override void ImplementRpcCallWithoutResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task/ValueTask
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ShadowClientEntity.ServerProxy)
            .GetMethods()
            .First(method => !method.IsGenericMethod && method.Name.Split("`")[0] == "Call")!;

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <inheritdoc/>
    protected override void ImplementRpcCallWithNotifyOnly(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        var attr = method.GetCustomAttribute<RpcStubNotifyOnlyAttribute>();
        var methodName = string.IsNullOrEmpty(attr?.RpcMethodName) ? method.Name : attr!.RpcMethodName;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ShadowClientEntity.ServerProxy)
            .GetMethod("Notify")!;

        Logger.Debug($"[ImplementRpcCallWithNotifyOnly] {method.Name} {methodName}");

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <inheritdoc/>
    protected override void GenerateRpcCall(FieldBuilder entityField, ILGenerator ilgenerator, string methodName, Type[] parameterTypes, MethodInfo callMethod)
    {
        // var [proxy] = this.entity.Server
        ilgenerator.DeclareLocal(typeof(object[]));

        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Ldfld, entityField);
        ilgenerator.Emit(
            OpCodes.Callvirt,
            this.EntityType.GetProperty("Server")!.GetGetMethod()!);

        // return proxy.Call<T>(methodName, arg0, arg1, arg2, ...);
        ilgenerator.Emit(OpCodes.Ldstr, methodName);

        ilgenerator.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
        ilgenerator.Emit(OpCodes.Newarr, typeof(object));

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            ilgenerator.Emit(OpCodes.Dup);
            ilgenerator.Emit(OpCodes.Ldc_I4, i);
            ilgenerator.Emit(OpCodes.Ldarg, i + 1);
            ilgenerator.Emit(OpCodes.Stelem_Ref);
        }

        ilgenerator.Emit(OpCodes.Stloc_0);
        ilgenerator.Emit(OpCodes.Ldloc_0);

        ilgenerator.Emit(OpCodes.Callvirt, callMethod);
        ilgenerator.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Validates the signature of an RPC method.
    /// </summary>
    /// <param name="methodInfo">The <see cref="MethodInfo"/> of the RPC method to validate.</param>
    /// <param name="notifyOnly">If generate notify-only RPC call.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
    protected override bool ValidateRpcMethodSignature(MethodInfo methodInfo, bool notifyOnly)
    {
        if (notifyOnly)
        {
            var returnType = methodInfo.ReturnType;
            if (returnType != typeof(void))
            {
                Logger.Warn($"RPC method {methodInfo.Name} on {methodInfo.Name} does not return void.");
                return false;
            }
        }

        return RpcHelper.ValidateMethodSignature(methodInfo, 0, notifyOnly);
    }
}