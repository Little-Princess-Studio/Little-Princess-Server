namespace LPS.Common.Core.Rpc;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class EntityClassAttribute : Attribute
{
    public readonly string Name;

    public EntityClassAttribute(string name = "")
    {
        this.Name = name;
    }
}