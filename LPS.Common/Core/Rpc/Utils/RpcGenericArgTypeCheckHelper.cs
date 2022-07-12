using LPS.Core.Rpc.RpcProperty;

namespace LPS.Core.Rpc.Utils;

public static class RpcGenericArgTypeCheckHelper
{
    public static void AssertIsValidKeyType<T>()
    {
        bool r = typeof(T) == typeof(int) ||
                 typeof(T) == typeof(string) ||
                 typeof(T) == typeof(bool) ||
                 typeof(T) == typeof(MailBox) ||
                 RpcHelper.IsValueTuple(typeof(T));

        if (!r)
        {
            throw new Exception($"Invalid Key Type {typeof(T)}");
        }
    }

    public static void AssertIsValidValueType<T>()
    {
        var type = typeof(T);
        if (type == typeof(int)
            || type == typeof(float)
            || type == typeof(string)
            || type == typeof(MailBox)
            || type == typeof(bool)
            || RpcHelper.IsTuple(typeof(T))
            || RpcHelper.IsValueTuple(typeof(T)))
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