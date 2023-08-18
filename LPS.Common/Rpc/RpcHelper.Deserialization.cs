// -----------------------------------------------------------------------
// <copyright file="RpcHelper.Deserialization.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Collections;
using System.Reflection;
using LPS.Common.Rpc.InnerMessages;
using Newtonsoft.Json;
using Any = Google.Protobuf.WellKnownTypes.Any;

/// <summary>
/// Provides helper methods for RPC (Remote Procedure Call) functionality.
/// </summary>
public static partial class RpcHelper
{
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
    /// Converts a collection of protobuf arguments to an array of RPC arguments based on the given method information.
    /// </summary>
    /// <param name="args">The collection of protobuf arguments to convert.</param>
    /// <param name="methodInfo">The method information to use for argument type information.</param>
    /// <returns>An array of RPC arguments.</returns>
    public static object?[] ProtobufArgsToRpcArgList(IEnumerable<Any> args, MethodInfo methodInfo)
    {
        var argTypes = methodInfo.GetParameters().Select(info => info.ParameterType).ToArray();
        return args
            .Select((elem, index) => ProtoBufAnyToRpcArg(elem, argTypes[index]))
            .ToArray();
    }

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
}