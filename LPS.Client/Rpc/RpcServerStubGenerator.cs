// -----------------------------------------------------------------------
// <copyright file="RpcServerStubGenerator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Rpc;

using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using LPS.Client.Entity;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Util;

/// <summary>
/// Provides methods for generating server-side implementations of RPC interfaces marked with RpcServerStubAttribute.
/// </summary>
public static class RpcServerStubGenerator
{
    private static ReadOnlyDictionary<uint, Type> rpcStubInterfaceIdToStubType = null!;

    /// <summary>
    /// Scans the assembly for RPC interfaces marked with RpcServerStubAttribute in the specified namespace and generates server-side implementations for them.
    /// </summary>
    /// <param name="namespace">The namespace to scan for RPC interfaces.</param>
    /// <exception cref="InvalidOperationException">Thrown when there are duplicate interface IDs.</exception>
    public static void ScanRpcServerStubInterfacesAndGenerateStubType(string @namespace)
    {
        var rpcStubInterfaceIdToStubTypeBuilder = new Dictionary<uint, Type>();
        var callingAssemblyTypes = Assembly.GetCallingAssembly()
            .GetTypes()
            .Where(type => type.Namespace == @namespace
                && type.IsInterface
                && type.IsDefined(typeof(RpcServerStubAttribute), inherit: false));
        var entryAssemblyTeyps = Assembly.GetEntryAssembly()!
            .GetTypes()
            .Where(type => type.Namespace == @namespace
                && type.IsInterface
                && type.IsDefined(typeof(RpcServerStubAttribute), inherit: false));
        IEnumerable<Type>? allInterfaces = callingAssemblyTypes.Concat(entryAssemblyTeyps).Distinct();

        foreach (var type in allInterfaces)
        {
            var interfaceId = TypeIdHelper.GetId(type);
            if (rpcStubInterfaceIdToStubTypeBuilder.ContainsKey(interfaceId))
            {
                throw new InvalidOperationException($"Duplicate interface ID {interfaceId}.");
            }

            var stubType = Generate(type);
            rpcStubInterfaceIdToStubTypeBuilder.Add(interfaceId, stubType);
        }

        rpcStubInterfaceIdToStubType = new ReadOnlyDictionary<uint, Type>(rpcStubInterfaceIdToStubTypeBuilder);
    }

    /// <summary>
    /// Generates a server-side implementation of the specified RPC interface.
    /// </summary>
    /// <typeparam name="T">The RPC interface to generate an implementation for.</typeparam>
    /// <returns>An instance of the generated implementation.</returns>
    /// <exception cref="ArgumentException">Thrown when T is not an interface type or is not marked with RpcServerStubAttribute.</exception>
    public static Type Generate<T>()
        where T : IRpcStub
    {
        var interfaceType = typeof(T);
        return Generate(interfaceType);
    }

    /// <summary>
    /// Generates a server-side implementation of the specified RPC interface.
    /// </summary>
    /// <param name="interfaceType">The RPC interface to generate an implementation for.</param>
    /// <returns>A <see cref="Type"/> object representing the generated implementation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="interfaceType"/> is not an interface type or is not marked with <see cref="RpcServerStubAttribute"/>.</exception>
    public static Type Generate(Type interfaceType)
    {
        if (!interfaceType.IsInterface)
        {
            throw new ArgumentException("T must be an interface type.");
        }

        if (!interfaceType.IsDefined(typeof(RpcServerStubAttribute), false))
        {
            throw new ArgumentException("T must be marked with RpcServerStubAttribute.");
        }

        var assemblyName = new AssemblyName(interfaceType.Name + "ImplAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(interfaceType.Name + "ImplModule");
        var typeBuilder = moduleBuilder.DefineType(interfaceType.Name + "Impl", TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.AddInterfaceImplementation(interfaceType);

        // Define readonly field for ShadowClientEntity
        var entityField =
            typeBuilder.DefineField("entity", typeof(ShadowClientEntity), FieldAttributes.Private | FieldAttributes.InitOnly);

        // Define constructor with ShadowClientEntity parameter
        GenerateConstructor(typeBuilder, entityField);

        // implement methods
        foreach (var method in interfaceType.GetMethods())
        {
            if (!ValidateRpcMethodSignature(method))
            {
                var e = new ArgumentException($"Method {method.Name} of {interfaceType.Name} has an invalid signature.");
                Logger.Error(e);
                throw e;
            }

            ImplementMethods(typeBuilder, entityField, method);
        }

        var generatedType = typeBuilder.CreateType()!;

        Logger.Init($"[RpcServerStubGenerator] Generated interface {interfaceType.FullName} with type {generatedType.FullName}");

        return generatedType;
    }

    private static ILGenerator ImplementMethods(TypeBuilder typeBuilder, FieldBuilder entityField, MethodInfo method)
    {
        var methodBuilder = typeBuilder.DefineMethod(
                        method.Name,
                        MethodAttributes.Public,
                        method.ReturnType,
                        method.GetParameters()
                        .Select(p => p.ParameterType).ToArray());
        var ilgenerator = methodBuilder.GetILGenerator();

        if (method.ReturnType.IsGenericType
            && (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                || method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            ImplementRpcCallWithResult(entityField, ilgenerator, method);
        }
        else
        {
            ImplementRpcCallWithoutResult(entityField, ilgenerator, method);
        }

        typeBuilder.DefineMethodOverride(methodBuilder, method);
        return ilgenerator;
    }

    private static void GenerateConstructor(TypeBuilder typeBuilder, FieldBuilder entityField)
    {
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new[] { typeof(ShadowClientEntity) });
        var ilgenerator = constructorBuilder.GetILGenerator();
        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Ldarg_1);
        ilgenerator.Emit(OpCodes.Stfld, entityField);
        ilgenerator.Emit(OpCodes.Ret);
    }

    private static void ImplementRpcCallWithResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task<T>/ValueTask<T>
        var returnType = method.ReturnType.GenericTypeArguments[0];
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ShadowClientEntity.ServerProxy)
            .GetMethod("Call")!
            .MakeGenericMethod(returnType);

        GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    private static void ImplementRpcCallWithoutResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task<T>/ValueTask<T>
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = typeof(ShadowClientEntity.ServerProxy)
            .GetMethod("Call")!;

        GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    private static void GenerateRpcCall(FieldBuilder entityField, ILGenerator ilgenerator, string methodName, Type[] parameterTypes, MethodInfo callMethod)
    {
        // var [proxy] = this.entity.Server
        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Ldfld, entityField);
        ilgenerator.Emit(
            OpCodes.Callvirt,
            typeof(ShadowClientEntity).GetProperty("Server")!.GetGetMethod()!);

        // return proxy.Call<T>(methodName, arg0, arg1, arg2, ...);
        ilgenerator.Emit(OpCodes.Ldstr, methodName);
        for (int i = 0; i < parameterTypes.Length; i++)
        {
            ilgenerator.Emit(OpCodes.Ldarg, i + 1);
        }

        ilgenerator.Emit(OpCodes.Callvirt, callMethod);
        ilgenerator.Emit(OpCodes.Ret);
    }

    private static bool ValidateRpcMethodSignature(MethodInfo methodInfo)
    {
        // todo: validate signature of rpc method.
        return true;
    }
}