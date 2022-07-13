using LPS.Core.Rpc.RpcProperty;

namespace LPS.Core.Rpc;

public static class RpcGenericArgTypeCheckHelper
{
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