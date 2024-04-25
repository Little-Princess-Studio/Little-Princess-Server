// -----------------------------------------------------------------------
// <copyright file="RpcStubGenerator.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Util;

/// <summary>
/// Generates RPC stubs.
/// </summary>
public abstract class RpcStubGenerator
{
    /// <summary>
    /// Gets the type of the entity that the generated RPC stubs will operate on.
    /// </summary>
    protected virtual Type EntityType => typeof(BaseEntity);

    /// <summary>
    /// Gets the type of the attribute used to mark RPC interfaces that should be generated as server-side implementations.
    /// </summary>
    protected virtual Type AttributeType => typeof(RpcStubAttribute);

    /// <summary>
    /// Gets or sets a read-only dictionary that maps RPC interface IDs to their corresponding server-side implementation types.
    /// </summary>
    protected ReadOnlyDictionary<uint, Type> RpcStubInterfaceIdToStubType { get; set; } = null!;

    /// <summary>
    /// Gets the server-side implementation <see cref="Type"/> of the specified RPC interface <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The RPC interface to get the implementation <see cref="Type"/> for.</typeparam>
    /// <param name="entity">The shadow client entity.</param>
    /// <returns>The server-side implementation <see cref="Type"/> of the specified RPC interface <typeparamref name="T"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified RPC interface <typeparamref name="T"/> is not found in the dictionary.</exception>
    public T GetRpcStubImpl<T>(BaseEntity entity)
        where T : class, IRpcStub
    {
        if (this.RpcStubInterfaceIdToStubType.TryGetValue(
            TypeIdHelper.GetId(typeof(T)),
            out var stubType))
        {
            return Activator.CreateInstance(stubType, entity) as T
                ?? throw new InvalidCastException($"Cannot cast {stubType} to {typeof(T)}.");
        }

        throw new KeyNotFoundException($"RpcStubImpl of {typeof(T)} not found.");
    }

    /// <summary>
    /// Scans the assembly for RPC interfaces marked with RpcServerStubAttribute in the specified namespace and generates server-side implementations for them.
    /// </summary>
    /// <param name="namespaces">The namespaces to scan for RPC interfaces.</param>
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    /// <exception cref="InvalidOperationException">Thrown when there are duplicate interface IDs.</exception>
    public virtual void ScanRpcServerStubInterfacesAndGenerateStubType(string[] namespaces, Assembly[]? extraAssemblies = null)
    {
        var rpcStubInterfaceIdToStubTypeBuilder = new Dictionary<uint, Type>();

        var assemblyName = new AssemblyName("RpcStubImplAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("RpcStubImplModule");

        foreach (var @namespace in namespaces)
        {
            var allInterfaces = AttributeHelper.ScanTypeWithNamespaceAndAttribute(
                @namespace,
                this.AttributeType,
                true,
                type => type.IsInterface,
                extraAssemblies);

            foreach (var type in allInterfaces)
            {
                var interfaceId = TypeIdHelper.GetId(type);
                if (rpcStubInterfaceIdToStubTypeBuilder.ContainsKey(interfaceId))
                {
                    throw new InvalidOperationException($"Duplicate interface ID {interfaceId}.");
                }

                var stubType = this.Generate(type, moduleBuilder);
                rpcStubInterfaceIdToStubTypeBuilder.Add(interfaceId, stubType);

                Logger.Info($"[RpcStubGenerator] Generated {stubType} for {type}.");
            }
        }

        this.RpcStubInterfaceIdToStubType = new ReadOnlyDictionary<uint, Type>(rpcStubInterfaceIdToStubTypeBuilder);
    }

    /// <summary>
    /// Generates a server-side implementation of the specified RPC interface.
    /// </summary>
    /// <param name="interfaceType">The RPC interface to generate an implementation for.</param>
    /// <param name="moduleBuilder">The <see cref="ModuleBuilder"/> to use for the generated implementation.</param>
    /// <returns>A <see cref="Type"/> object representing the generated implementation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="interfaceType"/> is not an interface type or is not marked with <see cref="RpcServerStubAttribute"/>.</exception>
    public virtual Type Generate(Type interfaceType, ModuleBuilder moduleBuilder)
    {
        if (!interfaceType.IsInterface)
        {
            throw new ArgumentException("T must be an interface type.");
        }

        if (!interfaceType.IsDefined(this.AttributeType, false))
        {
            throw new ArgumentException("T must be marked with RpcStubAttribute.");
        }

        var typeBuilder = moduleBuilder.DefineType(interfaceType.Name + "Impl", TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.AddInterfaceImplementation(interfaceType);

        // Define readonly field for BaseEntity
        var entityField =
            typeBuilder.DefineField("entity", this.EntityType, FieldAttributes.Private | FieldAttributes.InitOnly);

        // Define constructor with ShadowClientEntity parameter
        this.GenerateConstructor(typeBuilder, entityField);

        // implement methods
        var allInterfaceMethods = GetAllInterfaceMethods(interfaceType);
        foreach (var method in allInterfaceMethods)
        {
            Logger.Debug($"Implementing method {method.Name} for {interfaceType.Name + "ImplAssembly"}");
            var notifyOnly = method.IsDefined(typeof(RpcStubNotifyOnlyAttribute));
            if (!this.ValidateRpcMethodSignature(method, notifyOnly))
            {
                var e = new ArgumentException($"Method {method.Name} of {interfaceType.Name} has an invalid signature.");
                Logger.Error(e);
                throw e;
            }

            this.ImplementMethods(typeBuilder, entityField, method, notifyOnly);
        }

        var generatedType = typeBuilder.CreateType()!;

        Logger.Info($"[RpcStubGenerator] Generated interface {interfaceType.FullName} with type {generatedType.FullName}");

        return generatedType;
    }

    /// <summary>
    /// Implements the specified RPC method on the generated server-side implementation type.
    /// </summary>
    /// <param name="typeBuilder">The <see cref="TypeBuilder"/> for the generated implementation type.</param>
    /// <param name="entityField">The <see cref="FieldBuilder"/> for the BaseEntity field.</param>
    /// <param name="method">The <see cref="MethodInfo"/> for the RPC method to implement.</param>
    /// <param name="notifyOnly">If generate notify-only RPC call.</param>
    protected virtual void ImplementMethods(TypeBuilder typeBuilder, FieldBuilder entityField, MethodInfo method, bool notifyOnly)
    {
        var methodBuilder = typeBuilder.DefineMethod(
                        method.Name,
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        method.ReturnType,
                        method.GetParameters()
                        .Select(p => p.ParameterType).ToArray());
        var ilgenerator = methodBuilder.GetILGenerator();

        if (notifyOnly)
        {
            this.ImplementRpcCallWithNotifyOnly(entityField, ilgenerator, method);
        }
        else
        {
            if (method.ReturnType.IsGenericType
                && (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                    || method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            {
                this.ImplementRpcCallWithResult(entityField, ilgenerator, method);
            }
            else
            {
                this.ImplementRpcCallWithoutResult(entityField, ilgenerator, method);
            }
        }

        typeBuilder.DefineMethodOverride(methodBuilder, method);
    }

    /// <summary>
    /// Generates a constructor for the server-side implementation type that takes a <see cref="BaseEntity"/> parameter.
    /// </summary>
    /// <param name="typeBuilder">The <see cref="TypeBuilder"/> for the generated implementation type.</param>
    /// <param name="entityField">The <see cref="FieldBuilder"/> for the BaseEntity field.</param>
    protected virtual void GenerateConstructor(TypeBuilder typeBuilder, FieldBuilder entityField)
    {
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new[] { this.EntityType });
        var ilgenerator = constructorBuilder.GetILGenerator();
        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Ldarg_1);
        ilgenerator.Emit(OpCodes.Stfld, entityField);
        ilgenerator.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Implements the specified RPC method on the generated server-side implementation type that returns a result.
    /// </summary>
    /// <param name="entityField">The <see cref="FieldBuilder"/> for the BaseEntity field.</param>
    /// <param name="ilgenerator">The <see cref="ILGenerator"/> for the implemented method.</param>
    /// <param name="method">The <see cref="MethodInfo"/> for the RPC method to implement.</param>
    protected virtual void ImplementRpcCallWithResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task<T>/ValueTask<T>
        var returnType = method.ReturnType.GenericTypeArguments[0];
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = this.EntityType
            .GetMethods()
            .First(method => method.IsGenericMethod && method.Name.Split("`")[0] == "Call")!;

        AssertFirstParameterIsMailBox(parameterTypes);

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <summary>
    /// Implements the specified RPC method on the generated server-side implementation type that returns a notification only.
    /// </summary>
    /// <param name="entityField">The <see cref="FieldBuilder"/> for the BaseEntity field.</param>
    /// <param name="ilgenerator">The <see cref="ILGenerator"/> for the implemented method.</param>
    /// <param name="method">The <see cref="MethodInfo"/> for the RPC method to implement.</param>
    protected virtual void ImplementRpcCallWithNotifyOnly(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        var attr = method.GetCustomAttribute<RpcStubNotifyOnlyAttribute>();
        var methodName = string.IsNullOrEmpty(attr?.RpcMethodName) ? method.Name : attr!.RpcMethodName;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = this.EntityType
            .GetMethod("Notify")!;

        AssertFirstParameterIsMailBox(parameterTypes);

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <summary>
    /// Implements the specified RPC method on the generated server-side implementation type that does not return a result.
    /// </summary>
    /// <param name="entityField">The <see cref="FieldBuilder"/> for the BaseEntity field.</param>
    /// <param name="ilgenerator">The <see cref="ILGenerator"/> for the implemented method.</param>
    /// <param name="method">The <see cref="MethodInfo"/> for the RPC method to implement.</param>
    protected virtual void ImplementRpcCallWithoutResult(FieldBuilder entityField, ILGenerator ilgenerator, MethodInfo method)
    {
        // Task<T>/ValueTask<T>
        var methodName = method.Name;
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        var callMethod = this.EntityType
            .GetMethods()
            .First(method => !method.IsGenericMethod && method.Name.Split("`")[0] == "Call")!;

        AssertFirstParameterIsMailBox(parameterTypes);

        this.GenerateRpcCall(entityField, ilgenerator, methodName, parameterTypes, callMethod);
    }

    /// <summary>
    /// Generates IL code to call an RPC method on the specified entity field using the specified method name and parameter types.
    /// </summary>
    /// <param name="entityField">The <see cref="FieldBuilder"/> for the BaseEntity field.</param>
    /// <param name="ilgenerator">The <see cref="ILGenerator"/> for the implemented method.</param>
    /// <param name="methodName">The name of the RPC method to call.</param>
    /// <param name="parameterTypes">The types of the parameters for the RPC method.</param>
    /// <param name="callMethod">The <see cref="MethodInfo"/> for the BaseEntity.Call method to use for the RPC call.</param>
    protected virtual void GenerateRpcCall(FieldBuilder entityField, ILGenerator ilgenerator, string methodName, Type[] parameterTypes, MethodInfo callMethod)
    {
        // var [proxy] = this.entity
        ilgenerator.DeclareLocal(typeof(object[]));

        ilgenerator.Emit(OpCodes.Ldarg_0);
        ilgenerator.Emit(OpCodes.Ldfld, entityField);

        // load targetMailBox
        ilgenerator.Emit(OpCodes.Ldarg_1);

        // return proxy.Call<T>(targetMailBox, methodName, arg0, arg1, arg2, ...);
        ilgenerator.Emit(OpCodes.Ldstr, methodName);

        ilgenerator.Emit(OpCodes.Ldc_I4, parameterTypes.Length - 1);
        ilgenerator.Emit(OpCodes.Newarr, typeof(object));

        for (int i = 1; i < parameterTypes.Length; i++)
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
    protected virtual bool ValidateRpcMethodSignature(MethodInfo methodInfo, bool notifyOnly)
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

        var firstParameter = methodInfo.GetParameters().FirstOrDefault();

        if (firstParameter is null || firstParameter.ParameterType != typeof(MailBox))
        {
            Logger.Warn($"RPC method {methodInfo.Name} on {methodInfo.Name} does not have a MailBox parameter.");
            return false;
        }

        return RpcHelper.ValidateMethodSignature(methodInfo, 1, notifyOnly);
    }

    private static IEnumerable<MethodInfo> GetAllInterfaceMethods(Type interfaceType)
    {
        var methods = interfaceType.GetMethods().AsEnumerable();
        foreach (var parentInterface in interfaceType.GetInterfaces())
        {
            methods = methods.Concat(GetAllInterfaceMethods(parentInterface)).ToArray();
        }

        return methods;
    }

    private static void AssertFirstParameterIsMailBox(Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            throw new Exception("ParameterTypes is empty, at least one parameter `targetMailBox` is required.");
        }

        if (parameterTypes[0] != typeof(MailBox))
        {
            throw new Exception("First parameter's type must be `MailBox`.");
        }
    }
}
