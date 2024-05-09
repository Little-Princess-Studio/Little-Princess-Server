// -----------------------------------------------------------------------
// <copyright file="RpcHelper.Serialization.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using LPS.Common.Debug;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcStub;
using Newtonsoft.Json;
using Any = Google.Protobuf.WellKnownTypes.Any;

/// <summary>
/// Provides helper methods for RPC (Remote Procedure Call) functionality.
/// </summary>
public static partial class RpcHelper
{
    /// <summary>
    /// Serializes the property tree to a protobuf Any object.
    /// </summary>
    /// <param name="propTree">The property tree to serialize.</param>
    /// <returns>The serialized property tree as a protobuf Any object.</returns>
    public static Any SerializePropertyTree(Dictionary<string, RpcProperty.RpcProperty> propTree)
    {
        DictWithStringKeyArg? treeDict = new();

        Logger.Debug($"SerializePropertyTree: {propTree.Count}");
        foreach (var (key, value) in propTree)
        {
            Logger.Debug($"SerializePropertyTree: {key} {value.GetType()} {value.CanSyncToClient}");
            if (value.CanSyncToClient)
            {
                treeDict.PayLoad.Add(key, value.ToProtobuf());
            }
        }

        return Any.Pack(treeDict);
    }

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
            null => new NullArg(),
            bool b => new BoolArg { PayLoad = b },
            int i => new IntArg { PayLoad = i },
            float f => new FloatArg { PayLoad = f },
            string s => new StringArg { PayLoad = s },
            MailBox m => RpcMailBoxToPbMailBox(m),
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
        if (type == typeof(int) || type == typeof(int?)
            || type == typeof(float) || type == typeof(float?)
            || type == typeof(string)
            || type == typeof(MailBox) || type == typeof(MailBox?)
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
}