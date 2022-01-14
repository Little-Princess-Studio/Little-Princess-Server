using NLog;
using Logger = LPS.Core.Debug.Logger;

namespace LPS.Client
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string Name { get; set; }
        
        public ConsoleCommandAttribute(string name)
        {
            Name = name;
        }
    }
    
    public class AutoCompletionHandler : IAutoCompleteHandler
    {
        public string[] GetSuggestions(string text, int index)
        {
            throw new NotImplementedException();
        }

        public char[] Separators { get; set; } = { ' ', '.' };
    }
    
    public static class AutoCompleteConsole
    {
        public static void Init()
        {
            ReadLine.AutoCompletionHandler = new AutoCompletionHandler();
        }

        public static void Loop()
        {
            var input = ReadLine.Read("(command)>");
            while (input != "exit")
            {
                input = ReadLine.Read("(command)>");
            }
            
            Logger.Info("Bye.");
        }
    }
}
