using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Ipc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc
{
    public static class RpcHelper
    {
        public static MailBox PbMailBoxToRpcMailBox(InnerMessages.MailBox mb) => new(mb.ID, mb.IP, (int)mb.Port, (int)mb.HostNum);

        public static InnerMessages.MailBox RpcMailBoxToPbMailBox(MailBox mb) => new()
        {
            ID = mb.ID,
            IP = mb.IP,
            Port = (uint)mb.Port,
            HostNum = (uint)mb.HostNum
        };

        private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> RpcMethodInfo = new();
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
                        break;
                    }

                    if (messageBuf.TryRecieveFromRaw(buf, len, out var pkg))
                    {
                        var type = (PackageType)pkg.Header.Type;

                        var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
                        var arg = Tuple.Create(pb, conn, pkg.Header.ID);
                        var msg = new Message(type, arg);

                        Logger.Info($"msg recv: {msg.Key}");

                        onGotMessage(msg);
                    }
                }

                Logger.Debug("Connection Closed.");
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

            // connections_.Remove(conn);
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

        public static async Task<MailBox> CreateEntityLocally(string entityClassName, Dictionary<string, object> desc)
        {
            return null;
        }

        public static async Task<DistributeEntity> CreateEntityAnywhere()
        {
            return null;
        }

        #region Rpc method registration and validation
        public static void ScanRpcMethods(string namespaceName)
        {
            var types = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(type => type.IsClass && type.Namespace == namespaceName)
                    .Select(type => type)
                    .ToList();

            Logger.Info(
                "Init Rpc Types: ",
                string.Join(',', types.Select(type => type.Name).ToList())
            );

            types.ForEach(
                (type) =>
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
                                                        Logger.Warn($@"Invalid rpc method declaration: 
                                                            {methodInfo.ReturnType.Name} {methodInfo.Name}
                                                            ({string.Join(',', argTypes.Select(t => t.Name))})");
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
                        RpcMethodInfo[type] = rpcMethods;
                    }
                }
            );
        }

        private static bool ValidateArgs(Type[] args) => args.All(ValidateSingleArgType);

        private static bool ValidateSingleArgType(Type type)
        {
            if (type == typeof(int)
                || type == typeof(float)
                || type == typeof(string)
                || type == typeof(MailBox)
                || type == typeof(bool))
            {
                return true;
            }
            else if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var keyType = type.GetGenericArguments()[0];
                    var valueType = type.GetGenericArguments()[1];

                    return (keyType == typeof(string)
                            || valueType == typeof(int))
                            && ValidateSingleArgType(valueType);
                }
                else if (type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elemType = type.GetGenericArguments()[0];
                    return ValidateSingleArgType(elemType);
                }
            }

            return false;
        }
        #endregion

        #region Rpc serialization
        private static IMessage RpcListArgToProtoBuf(object list)
        {
            var itor = list as IEnumerable;
            var elemList = itor.Cast<object>().ToList();

            if (elemList.Count == 0)
            {
                return new NullArg();
            }

            var msg = new ListArg();

            foreach (var elem in elemList)
            {
                msg.PayLoad.Add(
                    Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(elem)));
            }

            return msg;
        }

        private static IMessage RpcDictArgToProtoBuf(object dict)
        {
            var itor = dict as IEnumerable;
            var kvList = itor.Cast<object>().ToList();

            if (kvList.Count == 0)
            {
                return new NullArg();
            }

            dynamic firstElem = kvList.First();
            var keyType = firstElem.Key.GetType();
            
            if (keyType == typeof(int))
            {
                var realDict = kvList.ToDictionary(
                                    kv => (int)((dynamic)kv).Key,
                                    kv => (object)((dynamic)kv).Value);

                var msg = new DictWithIntKeyArg();

                foreach (var pair in realDict)
                {
                    msg.PayLoad.Add(
                        pair.Key,
                        Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(pair.Value)));
                }

                return msg;
            }
            else
            {
                var realDict = kvList.ToDictionary(
                    kv => (string)((dynamic)kv).Key,
                    kv => (object)((dynamic)kv).Value);

                var msg = new DictWithStringKeyArg();

                foreach (var pair in realDict)
                {
                    msg.PayLoad.Add(
                        pair.Key,
                        Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(pair.Value)));
                }

                return msg;
            }
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
                bool b => new BoolArg() { PayLoad = b },
                int i => new IntArg() { PayLoad = i },
                float f => new FloatArg() { PayLoad = f },
                string s => new StringArg() { PayLoad = s },
                MailBox m => new MailBoxArg() { PayLoad = RpcMailBoxToPbMailBox(m) },
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) => RpcDictArgToProtoBuf(obj),
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) => RpcListArgToProtoBuf(obj),
                _ => throw new Exception($"Invalid Rpc arg type: {type.Name}")
            };
        }
        #endregion

        #region Rpc deserialization    
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

        private static object?[] ProtobufArgsToRpcArgList(EntityRpc entityRpc, MethodInfo methodInfo)
        {
            var methodArguments = methodInfo.GetGenericArguments();
            return entityRpc.Args
                .Select((elem, index) => ProtobufToRpcArg(elem, methodArguments[index]))
                .ToArray();
        }

        public static object? ProtobufToRpcArg(Google.Protobuf.WellKnownTypes.Any arg, Type argType)
        {
            object? obj = arg switch
            {
                _ when arg.Is(NullArg.Descriptor) => null,
                _ when arg.Is(IntArg.Descriptor) => arg.Unpack<IntArg>().PayLoad,
                _ when arg.Is(FloatArg.Descriptor) => arg.Unpack<FloatArg>().PayLoad,
                _ when arg.Is(StringArg.Descriptor) => arg.Unpack<StringArg>().PayLoad,
                _ when arg.Is(MailBoxArg.Descriptor) => PbMailBoxToRpcMailBox(arg.Unpack<MailBoxArg>().PayLoad),
                _ when arg.Is(DictWithStringKeyArg.Descriptor) => DictProtoBufToRpcArg(arg.Unpack<DictWithStringKeyArg>(), argType),
                _ when arg.Is(DictWithIntKeyArg.Descriptor) => DictProtoBufToRpcArg(arg.Unpack<DictWithIntKeyArg>(), argType),
                _ when arg.Is(ListArg.Descriptor) => ListProtoBufToRpcArg(arg.Unpack<ListArg>(), argType),
                _ => throw new Exception($"Invalid Rpc arg type: {arg.TypeUrl}"),
            };

            return obj;
        }
        #endregion

        public static EntityRpc  BuildRpcMessage(
            uint rpcID, string rpcMethodName, MailBox sender, MailBox target, params object?[] args)
        {
            var rpc = new EntityRpc()
            {
                RpcID = rpcID,
                SenderMailBox = RpcMailBoxToPbMailBox(sender),
                EntityMailBox = RpcMailBoxToPbMailBox(target),
                MethodName = rpcMethodName,
            };

            Array.ForEach(args, arg =>
            {
                rpc.Args.Add(Google.Protobuf.WellKnownTypes.Any.Pack(RpcArgToProtobuf(arg)));
            });

            return rpc;
        }

        public static MethodInfo GetRpcMethodArgTypes(Type type, string rpcMethodName)
        {
            return RpcMethodInfo[type][rpcMethodName];
        }
    }
}
