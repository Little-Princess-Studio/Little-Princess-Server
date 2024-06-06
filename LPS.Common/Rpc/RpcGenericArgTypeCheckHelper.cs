// -----------------------------------------------------------------------
// <copyright file="RpcGenericArgTypeCheckHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using LPS.Common.Rpc.RpcProperty.RpcContainer;

/// <summary>
/// Static checker for RPC generic argumetns.
/// </summary>
public static class RpcGenericArgTypeCheckHelper
{
    /// <summary>
    /// Assert if the key type is one of int/string/MailBox.
    /// </summary>
    /// <typeparam name="T">Key type.</typeparam>
    /// <exception cref="Exception">Throw exception if failed to check the key type.</exception>
    public static void AssertIsValidKeyType<T>()
    {
        bool r = typeof(T) == typeof(int) ||
                 typeof(T) == typeof(string) ||
                 typeof(T) == typeof(MailBox);

        // RpcHelper.IsValueTuple(typeof(T));
        // it's ard to impl value tuple as rpc property dict key, disable it currently
        if (!r)
        {
            throw new Exception($"Invalid Key Type {typeof(T)}");
        }
    }

    /// <summary>
    /// Assert if the type is plaint type (one of int/float/string/float/MailBox).
    /// </summary>
    /// <typeparam name="T">Type to check.</typeparam>
    /// <exception cref="Exception">Throw exception if failed to check the key type.</exception>
    public static void AssertIsValidPlaintType<T>()
    {
        var type = typeof(T);
        if (type == typeof(int)
            || type == typeof(float)
            || type == typeof(string)
            || type == typeof(MailBox)
            || type == typeof(bool))
        {
            return;
        }

        throw new Exception($"Invalid Plaint Type {typeof(T)}");
    }

    /// <summary>
    /// Assert if the value type is plaint type (one of int/float/string/float/MailBox) or RpcPropertyContainer type.
    /// </summary>
    /// <typeparam name="T">Key type.</typeparam>
    /// <exception cref="Exception">Throw exception if failed to check the key type.</exception>
    public static void AssertIsValidValueType<T>()
    {
        var type = typeof(T);
        if (type == typeof(int)
            || type == typeof(float)
            || type == typeof(string)
            || type == typeof(MailBox)
            || type == typeof(bool))
        {
            return;
        }

        if (type.IsSubclassOf(typeof(RpcPropertyContainer)))
        {
            return;
        }

        throw new Exception($"Invalid Value Type {typeof(T)}");
    }
}