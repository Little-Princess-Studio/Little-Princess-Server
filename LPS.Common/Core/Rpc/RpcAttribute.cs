namespace LPS.Core.Rpc
{
    public enum Authority
    {
        ServerOnly = 0x00000001,
        ClientOnly = 0x00000010,
        All = ServerOnly | ClientOnly,
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RpcMethodAttribute : Attribute
    {
        public Authority Authority;

        public RpcMethodAttribute(Authority authority)
        {
            Authority = authority;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RpcJsonTypeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class EntityClassAttribute : Attribute
    {
        public string Name;

        public EntityClassAttribute(string name = "")
        {
            this.Name = name;
        }
    }
}