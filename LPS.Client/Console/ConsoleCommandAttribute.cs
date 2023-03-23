namespace LPS.Client.Console;

/// <summary>
/// Tag a static method as console command.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ConsoleCommandAttribute : Attribute
{
    public string Name { get; }
        
    public ConsoleCommandAttribute(string name)
    {
        Name = name;
    }
}