// -----------------------------------------------------------------------
// <copyright file="RpcHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Collections;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Entity.Component;
using LPS.Common.Ipc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcStub;
using LPS.Common.Util;
using Newtonsoft.Json;
using Type = System.Type;

/// <summary>
/// Helper class for LPS Rpc.
/// </summary>
public static class RpcHelper
{
    /// <summary>
    /// Class mapping of class name and Type.
    /// </summary>
    public static readonly Dictionary<string, Type> EntityClassMap = new();

    /// <summary>
    /// RPC empty res.
    /// </summary>
    public static readonly object?[] EmptyRes = { null };

    private static ReadOnlyDictionary<uint, ReadOnlyDictionary<string, MethodInfo>> rpcMethodInfo = null!;

    private delegate RpcPropertyContainer RpcPropertyContainerDeserializeEntry(Any content);

    private static readonly Dictionary<Type, RpcPropertyContainerDeserializeEntry>
        RpcPropertyContainerDeserializeFactory = new(Util.TypeExtensions.GetTypeEqualityComparer());

    /// <summary>
    /// Register a Type as rpc container type.
    /// </summary>
    /// <param name="containerType">Container type.</param>
    /// <exception cref="Exception">Throw exception if failed to register rpc container type.</exception>
    public static void RegisterRpcPropertyContainer(Type containerType)
    {
        if (containerType.IsDefined(typeof(RpcPropertyContainerAttribute)))
        {
            var entry = containerType.GetMethods().Where(mtd =>
                    mtd.IsStatic && mtd.IsDefined(typeof(RpcPropertyContainerDeserializeEntryAttribute)))
                .FirstOrDefault(default(MethodInfo));

            if (entry == null)
            {
                // TODO: use RpcPropertyContainer.CreateSerializedContainer instead of throw exception
                throw new Exception($"RpcContainerType {entry} not have deserialize entry.");
            }

            if (entry.ReturnType != typeof(RpcPropertyContainer)
                || entry.GetParameters().Length != 1
                || entry.GetParameters()[0].ParameterType != typeof(Google.Protobuf.WellKnownTypes.Any))
            {
                throw new Exception(
                    $"Wrong signature of RpcContainerProperty deserialize entry method of {containerType}");
            }

            var deleg = Delegate.CreateDelegate(
                    typeof(RpcPropertyContainerDeserializeEntry),
                    entry)
                as RpcPropertyContainerDeserializeEntry;
            RegisterRpcPropertyContainerIntern(containerType, deleg!);
        }
    }

    /// <summary>
    /// Check if a type has been registered as rpc container type.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <returns>True if a type has been registered as rpc container type otherwise false.</returns>
    public static bool IsRpcContainerRegistered(Type type) =>
        RpcPropertyContainerDeserializeFactory.ContainsKey(type);

    /// <summary>
    /// Create RPC property container.
    /// </summary>
    /// <param name="type">RPC Container type.</param>
    /// <param name="content">Protobuf Any content to deserialize.</param>
    /// <returns>RPC property container.</returns>
    /// <exception cref="Exception">Throw exception if failed to create RPC property container.</exception>
    public static RpcPropertyContainer CreateRpcPropertyContainerByType(
        Type type,
        Google.Protobuf.WellKnownTypes.Any content)
    {
        if (RpcPropertyContainerDeserializeFactory.ContainsKey(type))
        {
            var gen = RpcPropertyContainerDeserializeFactory[type];
            return gen.Invoke(content);
        }

        throw new Exception($"No registered deserialize entry for type {type}");
    }

    /// <summary>
    /// Scan all the RPC Property Container tagged by <see cref="RpcPropertyContainerAttribute"/>.
    /// </summary>
    /// <param name="namespaceName">Namespace to scan.</param>
    /// <param name="assemblies">Optional array of assemblies to scan. If null, the calling assembly is used.</param>
    public static void ScanRpcPropertyContainer(string namespaceName, Assembly[]? assemblies = null)
    {
        var types = AttributeHelper.ScanTypeWithNamespaceAndAttribute(
            namespaceName,
            typeof(RpcPropertyContainerAttribute),
            false,
            type => type.IsClass,
            assemblies);

        Logger.Info(
            $"ScanRpcPropertyContainer in {namespaceName} types: {string.Join(',', types.Select(type => type.Name).ToArray())}");

        foreach (var type in types)
        {
            Logger.Info($"Register rpc property container : {type.FullName}");
            RegisterRpcPropertyContainer(type);
        }
    }

    /// <summary>
    /// Convert protobuf MailBox to RPC MailBox.
    /// </summary>
    /// <param name="mb">Protobuf MailBox.</param>
    /// <returns>RPC MailBox.</returns>
    public static MailBox PbMailBoxToRpcMailBox(InnerMessages.MailBox mb) =>
        new(mb.ID, mb.IP, (int)mb.Port, (int)mb.HostNum);

    /// <summary>
    /// Convert RPC MailBox to protobuf MailBox.
    /// </summary>
    /// <param name="mb">RPC MailBox.</param>
    /// <returns>Protobuf MailBox.</returns>
    public static InnerMessages.MailBox RpcMailBoxToPbMailBox(MailBox mb) => new()
    {
        ID = mb.Id,
        IP = mb.Ip,
        Port = (uint)mb.Port,
        HostNum = (uint)mb.HostNum,
    };

    /// <summary>
    /// Handle messages from remote.
    /// </summary>
    /// <param name="conn">Connection of remote.</param>
    /// <param name="stopCondition">Stop checker.</param>
    /// <param name="onGotMessage">Handler when receiving message.</param>
    /// <param name="onExitLoop">Handler when exiting.</param>
    /// <returns>Task.</returns>
    public static async Task HandleMessage(
        Connection conn,
        Func<bool> stopCondition,
        Action<Message> onGotMessage,
        Action? onExitLoop)
    {
        var buf = new byte[512];
        var messageBuf = new MessageBuffer();
        var socket = conn.Socket;

        try
        {
            while (conn.Status == ConnectStatus.Connected && !stopCondition())
            {
                var len = await socket.ReceiveAsync(buf, SocketFlags.None, conn.TokenSource.Token);

                if (len < 1)
                {
                    Logger.Info("Remote close the connection.");
                    break;
                }

                if (messageBuf.TryReceiveFromRaw(buf, len, out var pkg))
                {
                    var type = (PackageType)pkg.Header.Type;

                    var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
                    var arg = (pb, conn, pkg.Header.ID);
                    var msg = new Message(type, arg);
                    onGotMessage(msg);
                }
            }

            Logger.Debug($"Connection Closed. {conn.Status} {!stopCondition()}");
        }
        catch (OperationCanceledException ex)
        {
            Logger.Error(ex, "IO Task canceled, socket will close.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Read socket data failed, socket will close.");
        }

        onExitLoop?.Invoke();

        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Close socket failed");
        }
        finally
        {
            socket.Close();
        }
    }

    /// <summary>
    /// Build RPC message.
    /// </summary>
    /// <param name="rpcId">RPC id.</param>
    /// <param name="rpcMethodName">RPC method name.</param>
    /// <param name="sender">Who send.</param>
    /// <param name="target">Send to Who.</param>
    /// <param name="notifyOnly">If this message is for notification.</param>
    /// <param name="rpcType">Type of the RPC.</param>
    /// <param name="args">RPC arguments.</param>
    /// <returns>RPC protobuf object.</returns>
    public static EntityRpc BuildRpcMessage(
        uint rpcId,
        string rpcMethodName,
        MailBox sender,
        MailBox target,
        bool notifyOnly,
        RpcType rpcType,
        params object?[] args)
    {
        var rpc = new EntityRpc
        {
            RpcID = rpcId,
            SenderMailBox = RpcMailBoxToPbMailBox(sender),
            EntityMailBox = RpcMailBoxToPbMailBox(target),
            MethodName = rpcMethodName,
            NotifyOnly = notifyOnly,
            RpcType = rpcType,
        };

        Array.ForEach(
            args,
            arg => rpc.Args.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf(arg))));

        return rpc;
    }

    /// <summary>
    /// KeyCast is trick for generic full specialization like C++ for method KeyCast.
    /// KeyCast is used in <see cref="RpcDictionary{TK,TV}"/>.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <typeparam name="TK">Target type to cast.</typeparam>
    /// <returns>Cast key.</returns>
    public static TK KeyCast<TK>(string key)
    {
        return KeyCastSpecializeHelper.KeyCast<TK>(key);
    }

    /// <summary>
    /// Builds the property tree for a BaseEntity object.
    /// </summary>
    /// <param name="entity">The BaseEntity object to build the property tree for.</param>
    /// <param name="allowedRpcPropertyGenTypes">The set of allowed generic types for RPC properties.</param>
    public static void BuildPropertyTree(BaseEntity entity, HashSet<Type> allowedRpcPropertyGenTypes)
    {
        var type = entity.GetType();
        var tree = BuildPropertyTreeInternal(entity, type, allowedRpcPropertyGenTypes);

        foreach (var (_, prop) in tree)
        {
            prop.Owner = entity;
        }

        entity.SetPropertyTree(propertyTree: tree);
    }

    /// <summary>
    /// Serializes the property tree to a protobuf Any object.
    /// </summary>
    /// <param name="propTree">The property tree to serialize.</param>
    /// <returns>The serialized property tree as a protobuf Any object.</returns>
    public static Any SerializePropertyTree(Dictionary<string, RpcProperty.RpcProperty> propTree)
    {
        DictWithStringKeyArg? treeDict = new();

        foreach (var (key, value) in propTree)
        {
            if (value.CanSyncToClient)
            {
                treeDict.PayLoad.Add(key, value.ToProtobuf());
            }
        }

        return Any.Pack(treeDict);
    }

    /// <summary>
    /// Builds the property tree for a ComponentBase object.
    /// </summary>
    /// <param name="component">The ComponentBase object to build the property tree for.</param>
    /// <param name="allowedRpcPropertyGenTypes">The set of allowed generic types for RPC properties.</param>
    public static void BuildPropertyTree(ComponentBase component, HashSet<Type> allowedRpcPropertyGenTypes)
    {
        var type = component.GetType();
        var tree = BuildPropertyTreeInternal(component, type, allowedRpcPropertyGenTypes);

        foreach (var (_, prop) in tree)
        {
            prop.Owner = component.Owner;
            prop.IsComponentProperty = true;
            prop.OwnerComponent = component;
        }

        component.SetPropertyTree(tree);
    }

    #region Rpc container to protobuf any

    /// <summary>
    /// Convert RPC container list to protobuf Any object.
    /// </summary>
    /// <param name="list">RPC container list.</param>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerListToProtoBufAny(RpcList<string> list)
    {
        if (list.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        list.RawValue.ForEach(e => msg.PayLoad.Add(
            Any.Pack(GetRpcAny(((RpcPropertyContainer<string>)e).Value))));

        return msg;
    }

    /// <summary>
    /// Convert RPC container list to protobuf Any object.
    /// </summary>
    /// <param name="list">RPC container list.</param>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerListToProtoBufAny(RpcList<float> list)
    {
        if (list.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        list.RawValue.ForEach(e => msg.PayLoad.Add(
            GetRpcAny(((RpcPropertyContainer<float>)e).Value)));

        return msg;
    }

    /// <summary>
    /// Convert RPC container list to protobuf Any object.
    /// </summary>
    /// <param name="list">RPC container list.</param>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerListToProtoBufAny(RpcList<bool> list)
    {
        if (list.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        list.RawValue.ForEach(e => msg.PayLoad.Add(
            GetRpcAny(((RpcPropertyContainer<bool>)e).Value)));

        return msg;
    }

    /// <summary>
    /// Convert RPC container list to protobuf Any object.
    /// </summary>
    /// <param name="list">RPC container list.</param>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerListToProtoBufAny(RpcList<MailBox> list)
    {
        if (list.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        list.RawValue.ForEach(e => msg.PayLoad.Add(
            GetRpcAny(RpcMailBoxToPbMailBox((RpcPropertyContainer<MailBox>)e))));

        return msg;
    }

    /// <summary>
    /// Convert RPC container list to protobuf Any object.
    /// </summary>
    /// <param name="list">RPC container list.</param>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerListToProtoBufAny(RpcList<int> list)
    {
        if (list.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        list.RawValue.ForEach(e => msg.PayLoad.Add(
            GetRpcAny(((RpcPropertyContainer<int>)e).Value)));

        return msg;
    }

    /// <summary>
    /// Convert RPC container list to protobuf Any object.
    /// </summary>
    /// <param name="list">RPC container list.</param>
    /// <typeparam name="T">RpcList element type.</typeparam>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerListToProtoBufAny<T>(RpcList<T> list)
    {
        if (list.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        list.RawValue.ForEach(e => msg.PayLoad.Add(
            e.ToRpcArg()));

        return msg;
    }

#pragma warning disable SA1600
    public static IMessage RpcContainerDictToProtoBufAny<TK, TV>(RpcDictionary<TK, TV> dict)
#pragma warning restore SA1600
        where TK : notnull
    {
        throw new Exception($"Invalid Key Type: {typeof(TK)}");
    }

    /// <summary>
    /// Convert RPC container dictionary to protobuf Any object.
    /// </summary>
    /// <param name="dict">RPC container dictionary.</param>
    /// <typeparam name="TV">Value type.</typeparam>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerDictToProtoBufAny<TV>(RpcDictionary<int, TV> dict)
        where TV : notnull
    {
        if (dict.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new DictWithIntKeyArg();

        foreach (var (key, value) in dict.RawValue)
        {
            msg.PayLoad.Add(key, value.ToRpcArg());
        }

        return msg;
    }

    /// <summary>
    /// Convert RPC container dictionary to protobuf Any object.
    /// </summary>
    /// <param name="dict">RPC container dictionary.</param>
    /// <typeparam name="TV">Value type.</typeparam>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerDictToProtoBufAny<TV>(RpcDictionary<string, TV> dict)
        where TV : notnull
    {
        if (dict.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new DictWithStringKeyArg();

        foreach (var (key, value) in dict.RawValue)
        {
            msg.PayLoad.Add(key, value.ToRpcArg());
        }

        return msg;
    }

    /// <summary>
    /// Convert RPC container dictionary to protobuf Any object.
    /// </summary>
    /// <param name="dict">RPC container dictionary.</param>
    /// <typeparam name="TV">Value type.</typeparam>
    /// <returns>Protobuf Any object.</returns>
    public static IMessage RpcContainerDictToProtoBufAny<TV>(RpcDictionary<MailBox, TV> dict)
        where TV : notnull
    {
        if (dict.RawValue.Count == 0)
        {
            return new NullArg();
        }

        var msg = new DictWithMailBoxKeyArg();

        foreach (var (key, value) in dict.RawValue)
        {
            msg.PayLoad.Add(new DictWithMailBoxKeyPair
            {
                Key = RpcMailBoxToPbMailBox(key),
                Value = value.ToRpcArg(),
            });
        }

        return msg;
    }

    /// <summary>
    /// Converts a string value to a protobuf Any object.
    /// </summary>
    /// <param name="str">The string value to convert.</param>
    /// <returns>A protobuf Any object.</returns>
    public static Any GetRpcAny(string str) => Any.Pack(new StringArg() { PayLoad = str });

    /// <summary>
    /// Converts an integer value to a protobuf Any object.
    /// </summary>
    /// <param name="value">The integer value to convert.</param>
    /// <returns>A protobuf Any object.</returns>
    public static Any GetRpcAny(int value) => Any.Pack(new IntArg() { PayLoad = value });

    /// <summary>
    /// Converts a float value to a protobuf Any object.
    /// </summary>
    /// <param name="value">The float value to convert.</param>
    /// <returns>A protobuf Any object.</returns>
    public static Any GetRpcAny(float value) => Any.Pack(new FloatArg() { PayLoad = value });

    /// <summary>
    /// Converts a <see cref="Mailbox"/> value to a protobuf Any object.
    /// </summary>
    /// <param name="value">The <see cref="Mailbox"/> value to convert.</param>
    /// <returns>A protobuf Any object.</returns>
    public static Any GetRpcAny(in InnerMessages.MailBox value) => Any.Pack(new MailBoxArg() { PayLoad = value });

    /// <summary>
    /// Converts a boolean value to a protobuf Any object.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>A protobuf Any object.</returns>
    public static Any GetRpcAny(bool value) => Any.Pack(new BoolArg() { PayLoad = value });

    /// <summary>
    /// Extracts a string value from a protobuf Any object.
    /// </summary>
    /// <param name="any">The protobuf Any object to extract the string value from.</param>
    /// <returns>The extracted string value.</returns>
    public static string GetString(Any any)
    {
        var str = any.Unpack<StringArg>();
        return str.PayLoad;
    }

    /// <summary>
    /// Extracts an integer value from a protobuf Any object.
    /// </summary>
    /// <param name="any">The protobuf Any object to extract the integer value from.</param>
    /// <returns>The extracted integer value.</returns>
    public static int GetInt(Any any)
    {
        var str = any.Unpack<IntArg>();
        return str.PayLoad;
    }

    /// <summary>
    /// Extracts a float value from a protobuf Any object.
    /// </summary>
    /// <param name="any">The protobuf Any object to extract the float value from.</param>
    /// <returns>The extracted float value.</returns>
    public static float GetFloat(Any any)
    {
        var str = any.Unpack<FloatArg>();
        return str.PayLoad;
    }

    /// <summary>
    /// Extracts a boolean value from a protobuf Any object.
    /// </summary>
    /// <param name="any">The protobuf Any object to extract the boolean value from.</param>
    /// <returns>The extracted boolean value.</returns>
    public static bool GetBool(Any any)
    {
        var str = any.Unpack<BoolArg>();
        return str.PayLoad;
    }

    /// <summary>
    /// Extracts a <see cref="Mailbox"/> value from a protobuf Any object.
    /// </summary>
    /// <param name="any">The protobuf Any object to extract the <see cref="Mailbox"/> value from.</param>
    /// <returns>The extracted <see cref="Mailbox"/> value.</returns>
    public static InnerMessages.MailBox GetMailBox(Any any)
    {
        var str = any.Unpack<MailBoxArg>();
        return str.PayLoad;
    }

    #endregion

    #region Rpc method registration, validation, converter between ProtoBuf Any and LPS Rpc type.

    /// <summary>
    /// Scans all the RPC methods in the specified namespace and registers them.
    /// </summary>
    /// <param name="namespaceNames">The namespaces to scan for RPC methods.</param>
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    /// <exception cref="Exception">Thrown if there is an error while scanning or registering the RPC methods.</exception>
    public static void ScanRpcMethods(string[] namespaceNames, Assembly[]? extraAssemblies = null)
    {
        var tempRpcMethodInfo = new Dictionary<uint, ReadOnlyDictionary<string, MethodInfo>>();

        foreach (var namespaceName in namespaceNames)
        {
            var types =
                AttributeHelper.ScanTypeWithNamespaceAndAttribute(
                    namespaceName,
                    typeof(EntityClassAttribute),
                    false,
                    type => type.IsClass,
                    extraAssemblies)
                .ToList();

            Logger.Info(
                $"ScanRpcMethods in {namespaceName} types: {string.Join(',', types.Select(type => type.FullName).ToArray())}");

            types.ForEach(type =>
            {
                if (!type.IsSubclassOf(typeof(BaseEntity)))
                {
                    throw new Exception(
                        $"Invalid entity class {type}, entity class must inherit from BaseEntity class.");
                }

                var attrName = type.GetCustomAttribute<EntityClassAttribute>()!.Name;
                var regName = attrName != string.Empty ? attrName : type.Name;
                EntityClassMap[regName] = type;
                Logger.Info($"Register entity pair : {regName} {type}");
            });

            Logger.Info(
                "Init Rpc Types: ",
                string.Join(',', types.Select(type => type.Name).ToList()));

            types.ForEach(
                type =>
                {
                    var rpcMethods = type.GetMethods()
                        .Where(method => method.IsDefined(typeof(RpcMethodAttribute)))
                        .Select(method => method)
                        .ToDictionary(method => method.Name);
                    var dict = new ReadOnlyDictionary<string, MethodInfo>(rpcMethods);

                    var rpcArgValidation = rpcMethods.Values.All(m => ValidateMethodSignature(m, 0, false));

                    if (!rpcArgValidation)
                    {
                        var e = new Exception("Error when registering rpc methods.");
                        Logger.Fatal(e, string.Empty);

                        throw e;
                    }

                    if (rpcMethods.Count > 0)
                    {
                        var typeId = TypeIdHelper.GetId(type);
                        Logger.Info($"{type.Name} {typeId} register {string.Join(',', rpcMethods.Select(m => m.Key).ToList())}");
                        tempRpcMethodInfo[typeId] = dict;
                    }
                });
        }

        rpcMethodInfo = new(tempRpcMethodInfo);
    }

    /// <summary>
    /// Validates the signature of an RPC method.
    /// </summary>
    /// <param name="methodInfo">The <see cref="MethodInfo"/> object representing the RPC method.</param>
    /// <param name="startArgIdx">Starting index in the parameter list to check the parameter.</param>
    /// <param name="ignoreReturnType">Whether to ignore the return type of the method when validating the signature.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
    public static bool ValidateMethodSignature(MethodInfo methodInfo, int startArgIdx, bool ignoreReturnType)
    {
        var valid = false;
        if (methodInfo.Name == "OnResult")
        {
            valid = true;
        }
        else
        {
            var argTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            valid = ValidateArgs(argTypes[startArgIdx..]);

            if (!valid)
            {
                Logger.Warn($@"Args type invalid: invalid rpc method declaration: 
                                                            {methodInfo.ReturnType.Name} {methodInfo.Name}
                                                            ({string.Join(',', argTypes.Select(t => t.Name))})");
            }
        }

        var returnType = methodInfo.ReturnType;

        if (!ignoreReturnType)
        {
            if (returnType == typeof(Task)
                || returnType == typeof(ValueTask))
            {
                valid = valid && true;
            }
            else if (returnType.IsGenericType &&
                     (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                      || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            {
                var taskReturnType = returnType.GetGenericArguments()[0];
                valid = valid && ValidateRpcType(taskReturnType);
            }
            else
            {
                if (methodInfo.Name != "OnResult")
                {
                    valid = ValidateRpcType(returnType);
                }
                else
                {
                    Logger.Debug("BaseEntity::OnResult will not be checked");
                }
            }
        }

        if (!valid)
        {
            var argTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            Logger.Warn("Return type invalid: rpc method declaration:" +
                        $"{methodInfo.ReturnType.Name} {methodInfo.Name}" +
                        $"$({string.Join(',', argTypes.Select(t => t.Name))})");
        }

        return valid;
    }

    // public static RpcPropertyContainer ProtoBufAnyToRpcPropertyContainer<T>(
    //     Google.Protobuf.WellKnownTypes.Any content)
    // {
    //     do
    //     {
    //         if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(RpcList<>))
    //         {
    //             if (content.Is(NullArg.Descriptor))
    //             {
    //             }
    //
    //             if (!content.Is(ListArg.Descriptor))
    //             {
    //                 break;
    //             }
    //         }
    //         else if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(RpcDictionary<,>))
    //         {
    //             if (!content.Is(DictWithStringKeyArg.Descriptor))
    //             {
    //                 break;
    //             }
    //         }
    //         else
    //         {
    //             if (!content.Is(IntArg.Descriptor) && !content.Is(FloatArg.Descriptor) &&
    //                 !content.Is(StringArg.Descriptor)
    //                 && !content.Is(MailBoxArg.Descriptor) && !content.Is(BoolArg.Descriptor))
    //             {
    //                 break;
    //             }
    //         }
    //     }
    //     while (false);
    //
    //     throw new Exception("Deserialize failed.");
    // }

    /// <summary>
    /// Convert protobuf Any object to real object.
    /// </summary>
    /// <param name="arg">Protobuf any object.</param>
    /// <param name="argType">Type to convert.</param>
    /// <returns>Real object.</returns>
    /// <exception cref="Exception">Throw exception if failed to convert.</exception>
    public static object? ProtoBufAnyToRpcArg(Any arg, Type argType)
    {
        object? obj = arg switch
        {
            _ when arg.Is(NullArg.Descriptor) => null,
            _ when arg.Is(BoolArg.Descriptor) => GetBool(arg),
            _ when arg.Is(IntArg.Descriptor) => GetInt(arg),
            _ when arg.Is(FloatArg.Descriptor) => GetFloat(arg),
            _ when arg.Is(StringArg.Descriptor) => GetString(arg),
            _ when arg.Is(MailBoxArg.Descriptor) => PbMailBoxToRpcMailBox(GetMailBox(arg)),
            _ when arg.Is(JsonArg.Descriptor) => JsonConvert.DeserializeObject(
                arg.Unpack<JsonArg>().PayLoad,
                argType),
            _ when arg.Is(ValueTupleArg.Descriptor) => ValueTupleProtoBufToRpcArg(
                arg.Unpack<ValueTupleArg>(),
                argType),
            _ when arg.Is(TupleArg.Descriptor) => TupleProtoBufToRpcArg(arg.Unpack<TupleArg>(), argType),
            _ when arg.Is(DictWithStringKeyArg.Descriptor) => DictProtoBufToRpcArg(
                arg.Unpack<DictWithStringKeyArg>(), argType),
            _ when arg.Is(DictWithIntKeyArg.Descriptor) => DictProtoBufToRpcArg(
                arg.Unpack<DictWithIntKeyArg>(),
                argType),
            _ when arg.Is(DictWithValueTupleKeyArg.Descriptor) => DictProtoBufToRpcArg(
                arg.Unpack<DictWithValueTupleKeyArg>(), argType),
            _ when arg.Is(ListArg.Descriptor) => ListProtoBufToRpcArg(arg.Unpack<ListArg>(), argType),
            _ => throw new Exception($"Invalid Rpc arg type: {arg.TypeUrl}"),
        };

        return obj;
    }

    /// <summary>
    /// Convert real object to RPC argument.
    /// </summary>
    /// <param name="obj">Real object.</param>
    /// <returns>RPC argument.</returns>
    /// <exception cref="Exception">Throw exception if failed to convert.</exception>
    public static IMessage RpcArgToProtoBuf(object? obj)
    {
        if (obj == null)
        {
            return new NullArg();
        }

        var type = obj.GetType();
        return obj switch
        {
            bool b => new BoolArg { PayLoad = b },
            int i => new IntArg { PayLoad = i },
            float f => new FloatArg { PayLoad = f },
            string s => new StringArg { PayLoad = s },
            MailBox m => new MailBoxArg { PayLoad = RpcMailBoxToPbMailBox(m) },
            _ when type.IsDefined(typeof(RpcJsonTypeAttribute)) => new JsonArg
            { PayLoad = JsonConvert.SerializeObject(obj) },
            _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) =>
                RpcDictArgToProtoBuf(obj),
            _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) =>
                RpcListArgToProtoBuf(obj),
            _ when IsValueTuple(type) =>
                RpcValueTupleArgToProtoBuf(obj),
            _ when IsTuple(type) =>
                RpcTupleArgToProtoBuf(obj),
            _ => throw new Exception($"Invalid Rpc arg type: {type.FullName}"),
        };
    }

    /// <summary>
    /// Call local entity's RPC method.
    /// </summary>
    /// <param name="entity">Entity object.</param>
    /// <param name="entityRpc">RPC message.</param>
    public static void CallLocalEntity(BaseEntity entity, EntityRpc entityRpc)
    {
        // todo: impl jit to compile methodInfo.invoke to expression.invoke to improve perf.
        var methodInfo = GetRpcMethodArgTypes(entity.TypeId, entityRpc.MethodName);

        // OnResult is a special rpc method.
        if (entityRpc.MethodName == "OnResult")
        {
            methodInfo.Invoke(entity, new object?[] { entityRpc });
            return;
        }

        var args = ProtobufArgsToRpcArgList(entityRpc, methodInfo);

        object? res;
        try
        {
            res = methodInfo.Invoke(entity, args);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to call rpc method.");
            return;
        }

        // do not callback if rpc is notify-only
        if (entityRpc.NotifyOnly)
        {
            return;
        }

        var senderMailBox = entityRpc.SenderMailBox;

        var sendRpcType = RpcType.ServerInside;
        if (entityRpc.RpcType == RpcType.ClientToServer)
        {
            Logger.Info("rpc call is from client, the result will be sent to client.");
            sendRpcType = RpcType.ServerToClient;
        }
        else if (entityRpc.RpcType == RpcType.ServerToClient)
        {
            sendRpcType = RpcType.ClientToServer;
        }

        if (res != null)
        {
            HandleRpcMethodResult(entity, entityRpc, methodInfo, res, senderMailBox, sendRpcType);
        }
        else
        {
            entity.SendWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                "OnResult",
                true,
                sendRpcType,
                res);
        }
    }

    private static void HandleRpcMethodResult(
        BaseEntity entity,
        EntityRpc entityRpc,
        MethodInfo methodInfo,
        object res,
        InnerMessages.MailBox senderMailBox,
        RpcType sendRpcType)
    {
        var returnType = methodInfo.ReturnType;
        if (returnType.IsGenericType)
        {
            // TODO: for performance, need using IL instead of dynamic/reflection?
            if (returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                SendTaskResult(entity, entityRpc, senderMailBox, sendRpcType, res);
            }
            else if (returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                SendValueTaskResult(entity, entityRpc, senderMailBox, sendRpcType, res);
            }
        }
        else if (returnType == typeof(Task))
        {
            ((Task)res).ContinueWith(task =>
            {
                entity.SendWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    "OnResult",
                    true,
                    sendRpcType,
                    EmptyRes);
            });
        }
        else if (returnType == typeof(ValueTask))
        {
            var task = (ValueTask)res;
            if (task.IsCompleted)
            {
                entity.SendWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    "OnResult",
                    true,
                    sendRpcType,
                    EmptyRes);
            }
            else
            {
                // if ValueTask not complete, alloc awaiter to wait
                var awaiter = task.GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    entity.SendWithRpcId(
                        entityRpc.RpcID,
                        PbMailBoxToRpcMailBox(senderMailBox),
                        "OnResult",
                        true,
                        sendRpcType,
                        EmptyRes);
                });
            }
        }

        // var sendMethodInfo = entity.GetType().GetMethod("Send")!;
        // var continueWithExpMethodInfo = res.GetType().GetMethod("ContinueWith")!;
        // var resultFieldInfo = res.GetType().GetField("Result")!;
        //
        // var tParameter = Expression.Parameter(methodInfo.ReturnType.GetGenericArguments()[0], "t");
        // var sendLambda = Expression.Lambda(
        //     Expression.Call(
        //         Expression.Constant(entity),
        //         sendMethodInfo,
        //         Expression.Constant(RpcHelper.PbMailBoxToRpcMailBox(senderMailBox)),
        //         Expression.Constant("OnResult"),
        //         Expression.Field(tParameter, resultFieldInfo)),
        //     tParameter
        // );
        //
        // var precompiled = Expression.Lambda<Action>(
        //     Expression.Call(
        //         Expression.Constant(res),
        //         continueWithExpMethodInfo,
        //         sendLambda)).Compile();
        //
        // precompiled();
    }

    private static bool IsTuple(Type tuple)
    {
        if (!tuple.IsGenericType)
        {
            return false;
        }

        var openType = tuple.GetGenericTypeDefinition();
        return openType == typeof(Tuple<>)
               || openType == typeof(Tuple<,>)
               || openType == typeof(Tuple<,,>)
               || openType == typeof(Tuple<,,,>)
               || openType == typeof(Tuple<,,,,>)
               || openType == typeof(Tuple<,,,,,>)
               || openType == typeof(Tuple<,,,,,,>)
               || (openType == typeof(Tuple<,,,,,,,>) && IsTuple(tuple.GetGenericArguments()[7]));
    }

    private static bool IsValueTuple(Type tuple)
    {
        if (!tuple.IsGenericType)
        {
            return false;
        }

        var openType = tuple.GetGenericTypeDefinition();
        return openType == typeof(ValueTuple<>)
               || openType == typeof(ValueTuple<,>)
               || openType == typeof(ValueTuple<,,>)
               || openType == typeof(ValueTuple<,,,>)
               || openType == typeof(ValueTuple<,,,,>)
               || openType == typeof(ValueTuple<,,,,,>)
               || openType == typeof(ValueTuple<,,,,,,>)
               || (openType == typeof(ValueTuple<,,,,,,,>) && IsValueTuple(tuple.GetGenericArguments()[7]));
    }

    private static bool ValidateArgs(Type[] args) => args.Length == 0 || args.All(ValidateRpcType);

    private static bool ValidateRpcType(Type type)
    {
        if (type == typeof(int)
            || type == typeof(float)
            || type == typeof(string)
            || type == typeof(MailBox)
            || type == typeof(bool))
        {
            return true;
        }

        if (type.IsDefined(typeof(RpcJsonTypeAttribute)))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];

                return (keyType == typeof(string)
                        || keyType == typeof(int)
                        /*Allow ValueTuple as dict key*/
                        || (IsValueTuple(keyType)
                            && keyType.GetGenericArguments().All(ValidateRpcType)))
                       && ValidateRpcType(valueType);
            }

            if (type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = type.GetGenericArguments()[0];
                return ValidateRpcType(elemType);
            }

            if (IsTuple(type))
            {
                return type.GenericTypeArguments.All(ValidateRpcType);
            }

            if (IsValueTuple(type))
            {
                return type.GenericTypeArguments.All(ValidateRpcType);
            }
        }

        return false;
    }

    #endregion

    #region Rpc serialization

    private static IMessage RpcValueTupleArgToProtoBuf(object tuple)
    {
        var iobjTuple = (tuple as ITuple)!;

        var msg = new ValueTupleArg();
        for (int i = 0; i < iobjTuple.Length; ++i)
        {
            msg.PayLoad.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf(iobjTuple[i])));
        }

        return msg;
    }

    private static IMessage RpcTupleArgToProtoBuf(object tuple)
    {
        var iobjTuple = (tuple as ITuple)!;

        var msg = new TupleArg();
        for (int i = 0; i < iobjTuple.Length; ++i)
        {
            msg.PayLoad.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf(iobjTuple[i])));
        }

        return msg;
    }

    private static IMessage RpcListArgToProtoBuf(object list)
    {
        var enumerable = (list as IEnumerable)!;
        var elemList = enumerable.Cast<object>().ToList();

        if (elemList.Count == 0)
        {
            return new NullArg();
        }

        var msg = new ListArg();
        elemList.ForEach(e => msg.PayLoad.Add(
            Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf(e))));

        return msg;
    }

    private static IMessage RpcDictArgToProtoBuf(object dict)
    {
        var enumerable = (dict as IEnumerable)!;
        var pairList = enumerable.Cast<object>().ToList();

        if (pairList.Count == 0)
        {
            return new NullArg();
        }

        dynamic firstElem = pairList.First();
        Type keyType = firstElem.Key.GetType();

        if (keyType == typeof(int))
        {
            var realDict = pairList.ToDictionary(
                kv => (int)((dynamic)kv).Key,
                kv => (object)((dynamic)kv).Value);

            var msg = new DictWithIntKeyArg();

            foreach (var pair in realDict)
            {
                msg.PayLoad.Add(
                    pair.Key,
                    Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf(pair.Value)));
            }

            return msg;
        }

        if (keyType == typeof(string))
        {
            var realDict = pairList.ToDictionary(
                kv => (string)((dynamic)kv).Key,
                kv => (object)((dynamic)kv).Value);

            var msg = new DictWithStringKeyArg();

            foreach (var pair in realDict)
            {
                msg.PayLoad.Add(
                    pair.Key,
                    Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf(pair.Value)));
            }

            return msg;
        }

        if (IsValueTuple(keyType))
        {
            var realKvList = pairList.Select(kv => new DictWithValueTupleKeyPair
            {
                Key = Google.Protobuf.WellKnownTypes.Any.Pack(
                    RpcValueTupleArgToProtoBuf((object)((dynamic)kv).Key)),
                Value = Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtoBuf((object)((dynamic)kv).Value)),
            });

            var msg = new DictWithValueTupleKeyArg();

            msg.PayLoad.Add(realKvList);

            return msg;
        }

        throw new Exception($"Invalid dict key type {keyType}");
    }

    #endregion

    private static Dictionary<string, RpcProperty.RpcProperty> BuildPropertyTreeInternal(object obj, Type type, HashSet<Type> allowedRpcPropertyGenTypes)
    {
        var tree = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field =>
            {
                var fieldType = field.FieldType;

                var attr = field.GetCustomAttribute<RpcPropertyAttribute>();
                if (attr == null)
                {
                    return false;
                }

                if (!fieldType.IsGenericType)
                {
                    return false;
                }

                var genType = fieldType.GetGenericTypeDefinition();

                if (!allowedRpcPropertyGenTypes.Contains(type))
                {
                    return false;
                }

                var rpcProperty = field.GetValue(obj) as RpcProperty.RpcProperty;

                rpcProperty!.Init(attr.Name ?? fieldType.Name, attr.Setting);

                return true;
            }).ToDictionary(
                field => (field.GetValue(obj) as RpcProperty.RpcProperty)!.Name,
                field => (field.GetValue(obj) as RpcProperty.RpcProperty)!);
        return tree;
    }

    private static MethodInfo GetRpcMethodArgTypes(uint typeId, string rpcMethodName)
    {
        return rpcMethodInfo[typeId][rpcMethodName];
    }

    private static void SendValueTaskResult(
        BaseEntity entity,
        EntityRpc entityRpc,
        InnerMessages.MailBox senderMailBox,
        RpcType sendRpcType,
        in object res)
    {
        void SendDynamic(dynamic t) =>
            entity.SendWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                "OnResult",
                true,
                sendRpcType,
                t.Result);

        void Send<T>(in ValueTask<T> t) =>
            entity.SendWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                "OnResult",
                true,
                sendRpcType,
                t.Result);

        void HandleValueTask<T>(in ValueTask<T> task)
        {
            // ValueTask should always be sync
            if (task.IsCompleted)
            {
                Send(task);
            }
            else
            {
                // if ValueTask not complete, alloc awaiter to wait
                var awaiter = task.GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    entity.SendWithRpcId(
                        entityRpc.RpcID,
                        PbMailBoxToRpcMailBox(senderMailBox),
                        "OnResult",
                        true,
                        sendRpcType,
                        awaiter.GetResult());
                });
            }
        }

        void HandleValueTaskDynamic(dynamic task)
        {
            if (task.IsCompleted)
            {
                SendDynamic(task);
            }
            else
            {
                var awaiter = task.GetAwaiter();
                awaiter.OnCompleted(new Action(() =>
                {
                    entity.SendWithRpcId(
                        entityRpc.RpcID,
                        PbMailBoxToRpcMailBox(senderMailBox),
                        "OnResult",
                        true,
                        sendRpcType,
                        awaiter.GetResult());
                }));
            }
        }

        switch (res)
        {
            case ValueTask<int> task:
                HandleValueTask(task);
                break;
            case ValueTask<string> task:
                HandleValueTask(task);
                break;
            case ValueTask<float> task:
                HandleValueTask(task);
                break;
            case ValueTask<MailBox> task:
                HandleValueTask(task);
                break;
            case ValueTask<bool> task:
                HandleValueTask(task);
                break;
            default:
                {
                    dynamic task = res;
                    HandleValueTaskDynamic(task);
                }

                break;
        }
    }

    private static void SendTaskResult(
        BaseEntity entity,
        EntityRpc entityRpc,
        InnerMessages.MailBox senderMailBox,
        RpcType sendRpcType,
        in object res)
    {
        void SendDynamic(dynamic t) =>
            entity.SendWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                "OnResult",
                true,
                sendRpcType,
                t.Result);

        void Send<T>(Task<T> t) =>
            entity.SendWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                "OnResult",
                true,
                sendRpcType,
                t.Result);

        switch (res)
        {
            case Task<int> task:
                task.ContinueWith(Send);
                break;
            case Task<string> task:
                task.ContinueWith(Send);
                break;
            case Task<float> task:
                task.ContinueWith(Send);
                break;
            case Task<MailBox> task:
                task.ContinueWith(Send);
                break;
            case Task<bool> task:
                task.ContinueWith(Send);
                break;
            default:
                {
                    dynamic task = res;
                    task.ContinueWith((Action<dynamic>)SendDynamic);
                }

                break;
        }
    }

    private static void RegisterRpcPropertyContainerIntern(Type type, RpcPropertyContainerDeserializeEntry entry)
    {
        if (RpcPropertyContainerDeserializeFactory.ContainsKey(type))
        {
            Logger.Warn(
                $"Type already exist: {type}, register may duplicated (ignore this message if it's for generic rpc container type)");
        }

        RpcPropertyContainerDeserializeFactory[type] = entry;
    }

    #region Rpc deserialization

    private static object ValueTupleProtoBufToRpcArg(ValueTupleArg args, Type argType)
    {
        var tupleElemTypes = argType.GetGenericArguments();
        var objectArgs = args.PayLoad
            .Select((any, idx) => ProtoBufAnyToRpcArg(any, tupleElemTypes[idx]))
            .ToArray();

        var tuple = Activator.CreateInstance(argType, objectArgs)!;

        return tuple;
    }

    private static object TupleProtoBufToRpcArg(TupleArg args, Type argType)
    {
        var tupleElemTypes = argType.GetGenericArguments();
        var objectArgs = args.PayLoad
            .Select((any, idx) => ProtoBufAnyToRpcArg(any, tupleElemTypes[idx]))
            .ToArray();

        var tuple = Activator.CreateInstance(argType, objectArgs)!;

        return tuple;
    }

    private static object ListProtoBufToRpcArg(ListArg args, Type argType)
    {
        var list = Activator.CreateInstance(argType) as IList;

        foreach (var arg in args.PayLoad)
        {
            var obj = ProtoBufAnyToRpcArg(arg, argType);
            list!.Add(obj);
        }

        return list!;
    }

    private static object DictProtoBufToRpcArg(DictWithStringKeyArg arg, Type argType)
    {
        var dict = Activator.CreateInstance(argType) as IDictionary;
        var valueType = argType.GetGenericArguments()[1];

        foreach (var pair in arg.PayLoad)
        {
            dict![pair.Key] = ProtoBufAnyToRpcArg(pair.Value, valueType);
        }

        return dict!;
    }

    private static object DictProtoBufToRpcArg(DictWithIntKeyArg arg, Type argType)
    {
        var dict = Activator.CreateInstance(argType) as IDictionary;
        var valueType = argType.GetGenericArguments()[1];

        foreach (var pair in arg.PayLoad)
        {
            dict![pair.Key] = ProtoBufAnyToRpcArg(pair.Value, valueType);
        }

        return dict!;
    }

    private static object DictProtoBufToRpcArg(DictWithValueTupleKeyArg arg, Type argType)
    {
        var dict = Activator.CreateInstance(argType) as IDictionary;
        var keyType = argType.GetGenericArguments()[0];
        var valueType = argType.GetGenericArguments()[1];

        foreach (var pair in arg.PayLoad)
        {
            dict![ValueTupleProtoBufToRpcArg(pair.Key.Unpack<ValueTupleArg>(), keyType)] =
                ProtoBufAnyToRpcArg(pair.Value, valueType);
        }

        return dict!;
    }

    private static object?[] ProtobufArgsToRpcArgList(EntityRpc entityRpc, MethodInfo methodInfo)
    {
        var argTypes = methodInfo.GetParameters().Select(info => info.ParameterType).ToArray();
        return entityRpc.Args
            .Select((elem, index) => ProtoBufAnyToRpcArg(elem, argTypes[index]))
            .ToArray();
    }

    #endregion

    private static class KeyCastSpecializeHelper
    {
        private static class Impl<T>
        {
            public static Func<string, T>? Func;
        }

        static KeyCastSpecializeHelper()
        {
            Impl<int>.Func = Convert.ToInt32;
            Impl<string>.Func = key => key;
        }

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static TK KeyCast<TK>(string key)
        {
            if (Impl<TK>.Func == null)
            {
                throw new Exception($"Invalid key type {typeof(TK)}");
            }

            return Impl<TK>.Func(key);
        }
    }
}