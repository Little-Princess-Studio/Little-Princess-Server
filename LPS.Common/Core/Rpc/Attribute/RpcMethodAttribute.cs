namespace LPS.Common.Core.Rpc;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RpcMethodAttribute : Attribute
{
    public readonly Authority Authority;

    public RpcMethodAttribute(Authority authority)
    {
        Authority = authority;
    }
}