using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Ipc;
using LPS.Core.Rpc.InnerMessages;
using Newtonsoft.Json;

namespace LPS.Core.Rpc
{
    public static class RpcHelper
    {
        private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> RpcMethodInfo_ = new();
        public static readonly Dictionary<string, Type> EntityClassMap = new();

        public static readonly object?[] EmptyRes = new object?[] {null};
        
        public static MailBox PbMailBoxToRpcMailBox(InnerMessages.MailBox mb) =>
            new(mb.ID, mb.IP, (int) mb.Port, (int) mb.HostNum);

        public static InnerMessages.MailBox RpcMailBoxToPbMailBox(MailBox mb) => new()
        {
            ID = mb.Id,
            IP = mb.Ip,
            Port = (uint) mb.Port,
            HostNum = (uint) mb.HostNum
        };

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
                        var type = (PackageType) pkg.Header.Type;

                        var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
                        var arg = (pb, conn, pkg.Header.ID);
                        var msg = new Message(type, arg);

                        Logger.Info($"msg received: {pb}");

                        onGotMessage(msg);
                    }
                }

                Logger.Debug($"Connection Closed. {conn.Status} {!stopCondition()}");
            }
            catch (OperationCanceledException ex)
            {
                var ipEndPoint = socket.RemoteEndPoint as IPEndPoint;
                Logger.Error(ex, $"IO Task canceled {ipEndPoint!.Address} {ipEndPoint.Port}");
            }
            catch (Exception ex)
            {
                var ipEndPoint = socket.RemoteEndPoint as IPEndPoint;
                Logger.Error(ex, $"Read socket data failed, socket will close {ipEndPoint!.Address} {ipEndPoint.Port}");
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


        #region Rpc method registration and validation

        public static void ScanRpcMethods(string namespaceName)
        {
            var typesEntry = Assembly.GetCallingAssembly().GetTypes()
                .Where(
                    type => type.IsClass
                            && type.Namespace == namespaceName
                            && Attribute.IsDefined(type, typeof(EntityClassAttribute))
                );

            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(
                    type => type.IsClass
                            && type.Namespace == namespaceName
                            && Attribute.IsDefined(type, typeof(EntityClassAttribute))
                )
                .Concat(typesEntry)
                .Distinct()
                .ToList();

            Logger.Info($"types: {string.Join(',', types.Select(type => type.Name).ToArray())}");

            types.ForEach(type =>
            {
                if (!type.IsSubclassOf(typeof(BaseEntity)))
                {
                    throw new Exception(
                        $"Invalid entity class {type}, entity class must inherit from BaseEntity class.");
                }

                var attrName = type.GetCustomAttribute<EntityClassAttribute>()!.Name;
                var regName = attrName != "" ? attrName : type.Name;
                EntityClassMap[regName] = type;
                Logger.Info($"Register entity pair : {regName} {type}");
            });

            Logger.Info(
                "Init Rpc Types: ",
                string.Join(',', types.Select(type => type.Name).ToList())
            );

            types.ForEach(
                type =>
                {
                    var rpcMethods = type.GetMethods()
                        .Where(method => method.IsDefined(typeof(RpcMethodAttribute)))
                        .Select(method => method)
                        .ToDictionary(method => method.Name);

                    var rpcArgValidation = rpcMethods.Values.All(
                        methodInfo =>
                        {
                            var argTypes = methodInfo.GetGenericArguments();
                            var valid = ValidateArgs(argTypes);

                            if (!valid)
                            {
                                Logger.Warn($@"Args type invalid: invalid rpc method declaration: 
                                                            {methodInfo.ReturnType.Name} {methodInfo.Name}
                                                            ({string.Join(',', argTypes.Select(t => t.Name))})");
                            }

                            var returnType = methodInfo.ReturnType;

                            if (returnType == typeof(void)
                                || returnType == typeof(Task)
                                || returnType == typeof(ValueTask))
                            {
                                valid = true;
                            }
                            else if (returnType.IsGenericType &&
                                     (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                                      || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                            {
                                var taskReturnType = returnType.GetGenericArguments()[0];
                                valid = ValidateRpcType(taskReturnType);
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

                            if (!valid)
                            {
                                Logger.Warn("Return type invalid: rpc method declaration:" +
                                            $"{methodInfo.ReturnType.Name} {methodInfo.Name}" +
                                            $"$({string.Join(',', argTypes.Select(t => t.Name))})");
                            }

                            return valid;
                        });

                    if (!rpcArgValidation)
                    {
                        var e = new Exception("Error when registering rpc methods.");
                        Logger.Fatal(e, "");

                        throw e;
                    }

                    if (rpcMethods.Count > 0)
                    {
                        Logger.Info($"{type.Name} register {string.Join(',', rpcMethods.Select(m => m.Key).ToList())}");
                        RpcMethodInfo_[type] = rpcMethods;
                    }
                }
            );
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
                            || (keyType.IsGenericType
                                && keyType.GetGenericTypeDefinition() == typeof(ValueTuple<>)
                                && keyType.GetGenericArguments().All(ValidateRpcType))
                           )
                           && ValidateRpcType(valueType);
                }

                if (type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elemType = type.GetGenericArguments()[0];
                    return ValidateRpcType(elemType);
                }

                if (type.GetGenericTypeDefinition() == typeof(Tuple<>))
                {
                    return type.GenericTypeArguments.All(ValidateRpcType);
                }

                if (type.GetGenericTypeDefinition() == typeof(ValueTuple<>))
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
            var iTuple = (tuple as ITuple)!;

            var msg = new ValueTupleArg();
            for (int i = 0; i < iTuple.Length; ++i)
            {
                msg.PayLoad.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(iTuple[i])));
            }

            return msg;
        }

        private static IMessage RpcTupleArgToProtoBuf(object tuple)
        {
            var iTuple = (tuple as ITuple)!;

            var msg = new TupleArg();
            for (int i = 0; i < iTuple.Length; ++i)
            {
                msg.PayLoad.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(iTuple[i])));
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
                Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(e))));

            return msg;
        }

        private static IMessage RpcDictArgToProtoBuf(object dict)
        {
            var enumerable = (dict as IEnumerable)!;
            var kvList = enumerable.Cast<object>().ToList();

            if (kvList.Count == 0)
            {
                return new NullArg();
            }

            dynamic firstElem = kvList.First();
            Type keyType = firstElem.Key.GetType();

            if (keyType == typeof(int))
            {
                var realDict = kvList.ToDictionary(
                    kv => (int) ((dynamic) kv).Key,
                    kv => (object) ((dynamic) kv).Value);

                var msg = new DictWithIntKeyArg();

                foreach (var pair in realDict)
                {
                    msg.PayLoad.Add(
                        pair.Key,
                        Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(pair.Value)));
                }

                return msg;
            }

            if (keyType == typeof(string))
            {
                var realDict = kvList.ToDictionary(
                    kv => (string) ((dynamic) kv).Key,
                    kv => (object) ((dynamic) kv).Value);

                var msg = new DictWithStringKeyArg();

                foreach (var pair in realDict)
                {
                    msg.PayLoad.Add(
                        pair.Key,
                        Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(pair.Value)));
                }

                return msg;
            }

            if (keyType.IsGenericType && keyType.GetGenericTypeDefinition() == typeof(ValueTuple<>))
            {
                var realKvList = kvList.Select(kv => new DictWithValueTupleKeyPair
                {
                    Key = Google.Protobuf.WellKnownTypes.Any.Pack(
                        RpcValueTupleArgToProtoBuf((object) ((dynamic) kv).Key)),
                    Value = Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf((object) ((dynamic) kv).Value)),
                });

                var msg = new DictWithValueTupleKeyArg();

                msg.PayLoad.Add(realKvList);

                return msg;
            }

            throw new Exception($"Invalid dict key type {keyType}");
        }

        private static IMessage RpcArgToProtobuf(object? obj)
        {
            if (obj == null)
            {
                return new NullArg();
            }

            var type = obj.GetType();
            return obj switch
            {
                bool b => new BoolArg {PayLoad = b},
                int i => new IntArg {PayLoad = i},
                float f => new FloatArg {PayLoad = f},
                string s => new StringArg {PayLoad = s},
                MailBox m => new MailBoxArg {PayLoad = RpcMailBoxToPbMailBox(m)},
                _ when type.IsDefined(typeof(RpcJsonTypeAttribute)) => new JsonArg
                    {PayLoad = JsonConvert.SerializeObject(obj)},
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) =>
                    RpcDictArgToProtoBuf(obj),
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) =>
                    RpcListArgToProtoBuf(obj),
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTuple<>) =>
                    RpcValueTupleArgToProtoBuf(obj),
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Tuple<>) =>
                    RpcTupleArgToProtoBuf(obj),
                _ => throw new Exception($"Invalid Rpc arg type: {type.Name}")
            };
        }

        #endregion

        #region Rpc deserialization

        private static object ValueTupleProtoBufToRpcArg(ValueTupleArg args, Type argType)
        {
            var tupleElemTypes = argType.GetGenericArguments();
            var objectArgs = args.PayLoad
                .Select((any, idx) => ProtobufToRpcArg(any, tupleElemTypes[idx]))
                .ToArray();

            var tuple = Activator.CreateInstance(argType, objectArgs)!;

            return tuple;
        }

        private static object TupleProtoBufToRpcArg(TupleArg args, Type argType)
        {
            var tupleElemTypes = argType.GetGenericArguments();
            var objectArgs = args.PayLoad
                .Select((any, idx) => ProtobufToRpcArg(any, tupleElemTypes[idx]))
                .ToArray();

            var tuple = Activator.CreateInstance(argType, objectArgs)!;

            return tuple;
        }

        private static object ListProtoBufToRpcArg(ListArg args, Type argType)
        {
            var list = Activator.CreateInstance(argType) as IList;

            foreach (var arg in args.PayLoad)
            {
                var obj = ProtobufToRpcArg(arg, argType);
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
                dict![pair.Key] = ProtobufToRpcArg(pair.Value, valueType);
            }

            return dict!;
        }

        private static object DictProtoBufToRpcArg(DictWithIntKeyArg arg, Type argType)
        {
            var dict = Activator.CreateInstance(argType) as IDictionary;
            var valueType = argType.GetGenericArguments()[1];

            foreach (var pair in arg.PayLoad)
            {
                dict![pair.Key] = ProtobufToRpcArg(pair.Value, valueType);
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
                    ProtobufToRpcArg(pair.Value, valueType);
            }

            return dict!;
        }

        private static object?[] ProtobufArgsToRpcArgList(EntityRpc entityRpc, MethodInfo methodInfo)
        {
            var argTypes = methodInfo.GetParameters().Select(info => info.GetType()).ToArray();
            return entityRpc.Args
                .Select((elem, index) => ProtobufToRpcArg(elem, argTypes[index]))
                .ToArray();
        }

        public static object? ProtobufToRpcArg(Google.Protobuf.WellKnownTypes.Any arg, Type argType)
        {
            object? obj = arg switch
            {
                _ when arg.Is(NullArg.Descriptor) => null,
                _ when arg.Is(BoolArg.Descriptor) => arg.Unpack<BoolArg>().PayLoad,
                _ when arg.Is(IntArg.Descriptor) => arg.Unpack<IntArg>().PayLoad,
                _ when arg.Is(FloatArg.Descriptor) => arg.Unpack<FloatArg>().PayLoad,
                _ when arg.Is(StringArg.Descriptor) => arg.Unpack<StringArg>().PayLoad,
                _ when arg.Is(MailBoxArg.Descriptor) => PbMailBoxToRpcMailBox(arg.Unpack<MailBoxArg>().PayLoad),
                _ when arg.Is(JsonArg.Descriptor) => JsonConvert.DeserializeObject(arg.Unpack<JsonArg>().PayLoad,
                    argType),
                _ when arg.Is(ValueTupleArg.Descriptor) => ValueTupleProtoBufToRpcArg(arg.Unpack<ValueTupleArg>(),
                    argType),
                _ when arg.Is(TupleArg.Descriptor) => TupleProtoBufToRpcArg(arg.Unpack<TupleArg>(), argType),
                _ when arg.Is(DictWithStringKeyArg.Descriptor) => DictProtoBufToRpcArg(
                    arg.Unpack<DictWithStringKeyArg>(), argType),
                _ when arg.Is(DictWithIntKeyArg.Descriptor) => DictProtoBufToRpcArg(arg.Unpack<DictWithIntKeyArg>(),
                    argType),
                _ when arg.Is(DictWithValueTupleKeyArg.Descriptor) => DictProtoBufToRpcArg(
                    arg.Unpack<DictWithValueTupleKeyArg>(), argType),
                _ when arg.Is(ListArg.Descriptor) => ListProtoBufToRpcArg(arg.Unpack<ListArg>(), argType),
                _ => throw new Exception($"Invalid Rpc arg type: {arg.TypeUrl}"),
            };

            return obj;
        }

        #endregion

        public static EntityRpc BuildRpcMessage(
            uint rpcId, string rpcMethodName, MailBox sender, MailBox target, bool notifyOnly, params object?[] args)
        {
            var rpc = new EntityRpc
            {
                RpcID = rpcId,
                SenderMailBox = RpcMailBoxToPbMailBox(sender),
                EntityMailBox = RpcMailBoxToPbMailBox(target),
                MethodName = rpcMethodName,
                NotifyOnly = notifyOnly,
            };

            Array.ForEach(args,
                arg => rpc.Args.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(arg))));

            return rpc;
        }

        private static MethodInfo GetRpcMethodArgTypes(Type type, string rpcMethodName)
        {
            return RpcMethodInfo_[type][rpcMethodName];
        }

        public static void CallLocalEntity(BaseEntity entity, EntityRpc entityRpc)
        {
            var methodInfo = GetRpcMethodArgTypes(entity.GetType(), entityRpc.MethodName);

            // OnResult is a special rpc method.
            if (entityRpc.MethodName == "OnResult")
            {
                methodInfo.Invoke(entity, new object?[] {entityRpc});
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

            // for Dict/List/ValueTuple/Tuple Type
            
            if (res != null)
            {
                var returnType = methodInfo.ReturnType;
                if (returnType.IsGenericType)
                {
                    // TODO: for performance, need using IL instead of dynamic/reflection?
                    if (returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        SendTaskResult(entity, entityRpc, senderMailBox, res);
                    }
                    else if (returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                    {
                        SendValueTaskResult(entity, entityRpc, senderMailBox, res);
                    }
                }
                else if (returnType == typeof(Task))
                {
                    ((Task) res).ContinueWith(task =>
                    {
                        entity.SendWithRpcId(
                            entityRpc.RpcID,
                            PbMailBoxToRpcMailBox(senderMailBox),
                            "OnResult",
                            true,
                            EmptyRes);
                    });
                }
                else if (returnType == typeof(ValueTask))
                {
                    var task = (ValueTask) res;
                    if (task.IsCompleted)
                    {
                        entity.SendWithRpcId(
                            entityRpc.RpcID,
                            PbMailBoxToRpcMailBox(senderMailBox),
                            "OnResult",
                            true,
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
            else
            {
                entity.SendWithRpcId(entityRpc.RpcID, PbMailBoxToRpcMailBox(senderMailBox), "OnResult", true, res);
            }
        }

        private static void SendValueTaskResult(BaseEntity entity, EntityRpc entityRpc,
            InnerMessages.MailBox senderMailBox, in object res)
        {
            void SendDynamic(dynamic t) =>
                entity.SendWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    "OnResult",
                    true,
                    t.Result);

            void Send<T>(in ValueTask<T> t) =>
                entity.SendWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    "OnResult",
                    true,
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

        private static void SendTaskResult(BaseEntity entity, EntityRpc entityRpc, InnerMessages.MailBox senderMailBox,
            in object res)
        {
            void SendDynamic(dynamic t) =>
                entity.SendWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    "OnResult",
                    true,
                    t.Result);

            void Send<T>(Task<T> t) =>
                entity.SendWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    "OnResult",
                    true,
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
                    task.ContinueWith((Action<dynamic>) SendDynamic);
                }
                    break;
            }
        }
    }
}