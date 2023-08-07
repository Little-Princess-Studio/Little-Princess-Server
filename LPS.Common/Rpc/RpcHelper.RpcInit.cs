// -----------------------------------------------------------------------
// <copyright file="RpcHelper.RpcInit.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
#define USE_PIPE

namespace LPS.Common.Rpc;

using System.Buffers;
using System.Collections.ObjectModel;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Reflection;
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
using Type = System.Type;

/// <summary>
/// Helper class for LPS Rpc.
/// </summary>
public static partial class RpcHelper
{
    /// <summary>
    /// Class mapping of class name and Type.
    /// </summary>
    public static readonly Dictionary<string, Type> EntityClassMap = new();

    /// <summary>
    /// RPC empty res.
    /// </summary>
    public static readonly object?[] EmptyRes = { null };

    private static ReadOnlyDictionary<uint, ReadOnlyDictionary<string, RpcMethodDescriptor>> rpcMethodInfo = null!;

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
#if USE_PIPE
        Logger.Debug("Use Pipe: true");
        await HandleMessageWithPipe(conn, stopCondition, onGotMessage, onExitLoop);
#else
        Logger.Debug("Use Pipe: false");
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
#endif
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

    /// <summary>
    /// Scans all the RPC methods in the specified namespace and registers them.
    /// </summary>
    /// <param name="namespaceNames">The namespaces to scan for RPC methods.</param>
    /// <param name="extraAssemblies">Optional extra assemblies to include in the scan.</param>
    /// <exception cref="Exception">Thrown if there is an error while scanning or registering the RPC methods.</exception>
    public static void ScanRpcMethods(string[] namespaceNames, Assembly[]? extraAssemblies = null)
    {
        var tempRpcMethodInfo = new Dictionary<uint, ReadOnlyDictionary<string, RpcMethodDescriptor>>();

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
                        .Select(method => new RpcMethodDescriptor(method, method.GetCustomAttribute<RpcMethodAttribute>()!.Authority))
                        .ToDictionary(method => method.MethodName);
                    var dict = new ReadOnlyDictionary<string, RpcMethodDescriptor>(rpcMethods);

                    var rpcArgValidation = rpcMethods.Values.All(m => ValidateMethodSignature(m.Method, 0, false));

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

    /// <summary>
    /// Handle messages from remote using a pipe.
    /// </summary>
    /// <param name="conn">Connection of remote.</param>
    /// <param name="stopCondition">Stop checker.</param>
    /// <param name="onGotMessage">Handler when receiving message.</param>
    /// <param name="onExitLoop">Handler when exiting.</param>
    /// <returns>Task.</returns>
    private static async Task HandleMessageWithPipe(
        Connection conn,
        Func<bool> stopCondition,
        Action<Message> onGotMessage,
        Action? onExitLoop)
    {
        var socket = conn.Socket;

        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;

        await Task.WhenAll(
            FillPipeAsync(writer, conn, stopCondition),
            ReadPipeAsync(reader, conn, onGotMessage, stopCondition)).ContinueWith((t) =>
            {
                if (t.Exception != null)
                {
                    Logger.Error(t.Exception, "HandleMessageWithPipe error");
                }
            });

        Logger.Debug("Exit HandleMessageWithPipe");
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

    private static async Task FillPipeAsync(PipeWriter writer, Connection conn, Func<bool> stopCondition)
    {
        const int minimumBufferSize = 512;

        var socket = conn.Socket;
        var cancelTokenSource = conn.TokenSource;

        while (conn.Status == ConnectStatus.Connected && !stopCondition.Invoke())
        {
            var memory = writer.GetMemory(minimumBufferSize);
            try
            {
                var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, cancelTokenSource.Token);
                Logger.Info($"Read {bytesRead} bytes from socket.");
                if (bytesRead == 0)
                {
                    Logger.Info("Remote close the connection.");
                    break;
                }

                writer.Advance(bytesRead);
            }
            catch (OperationCanceledException ex)
            {
                Logger.Error(ex, "IO Task canceled, socket will close.");
                break;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Read socket data failed, socket will close.");
                break;
            }

            // Make the data available to the PipeReader
            _ = await writer.FlushAsync();

            // if (result.IsCompleted)
            // {
            //     Logger.Debug("Reader no longger read data from writer.");
            //     break;
            // }
        }

        Logger.Debug("Writer complete.");
        writer.Complete();
    }

    private static async Task ReadPipeAsync(PipeReader reader, Connection conn, Action<Message> onGotMessage, Func<bool> stopCondition)
    {
        while (conn.Status == ConnectStatus.Connected && !stopCondition.Invoke())
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            while (buffer.Length > PackageHeader.Size)
            {
                var pkgLen = GetPackageLength(ref buffer);

                if (buffer.Length >= pkgLen)
                {
                    var bytesToParse = buffer.Slice(buffer.Start, pkgLen);

                    var pkg = PackageHelper.GetPackage(
                        ref bytesToParse);

                    PackageType type = (PackageType)pkg.Header.Type;
                    var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
                    var arg = (pb, conn, pkg.Header.ID);
                    var msg = new Message(type, arg);
                    onGotMessage(msg);

                    buffer = buffer.Slice(bytesToParse.End);
                    reader.AdvanceTo(bytesToParse.End);
                }
                else
                {
                    break;
                }
            }

            // if (result.IsCompleted)
            // {
            //     Logger.Debug("Writer no longger write data to reader.");
            //     break;
            // }
        }

        reader.Complete();

        static ushort GetPackageLength(ref ReadOnlySequence<byte> buffer)
        {
            var bytesToParse = buffer.Slice(buffer.Start, 2).FirstSpan;
            var pkgLen = BitConverter.ToUInt16(bytesToParse);
            return pkgLen;
        }
    }

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

                if (!allowedRpcPropertyGenTypes.Contains(genType))
                {
                    return false;
                }

                var rpcProperty = field.GetValue(obj) as RpcProperty.RpcProperty;

                rpcProperty!.Init(attr.Name ?? fieldType.Name, attr.Setting);

                Logger.Debug($"RpcProperty {rpcProperty.Name} in {obj.GetType().Name} {attr.Setting} {rpcProperty.CanSyncToClient}");
                return true;
            }).ToDictionary(
                field => (field.GetValue(obj) as RpcProperty.RpcProperty)!.Name,
                field => (field.GetValue(obj) as RpcProperty.RpcProperty)!);
        return tree;
    }

    private static RpcMethodDescriptor GetRpcMethodArgTypes(uint typeId, string rpcMethodName)
    {
        return rpcMethodInfo[typeId][rpcMethodName];
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

    private readonly struct RpcMethodDescriptor
    {
        public readonly MethodInfo Method;

        public readonly Authority Authority;

        public string MethodName => this.Method.Name;

        public RpcMethodDescriptor(MethodInfo methodInfo, Authority authority)
        {
            this.Method = methodInfo;
            this.Authority = authority;
        }
    }
}