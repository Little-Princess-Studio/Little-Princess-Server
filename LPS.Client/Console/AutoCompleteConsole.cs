using Mono.Terminal;
using Logger = LPS.Common.Core.Debug.Logger;

namespace LPS.Client.Console
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string Name { get; }
        
        public ConsoleCommandAttribute(string name)
        {
            Name = name;
        }
    }

    public static class AutoCompleteConsole
    {
        private static LineEditor? LineEditor_;
        
        public static void Init()
        {
            LineEditor_ = new LineEditor ("cmd") {
                HeuristicsMode = "csharp"
            };
            
            LineEditor_.AutoCompleteEvent += delegate (string text, int _){
                var cmdArray = text.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (cmdArray.Length > 0)
                {
                    var cmdName = cmdArray[0];
                    var (cmdNames, _) = CommandParser.FindSuggestions(cmdName);

                    LineEditor_.TabAtStartCompletes = true;
                    return new LineEditor.Completion (cmdName, 
                        cmdNames.Select(cmd => cmd.Substring(cmdName
                    .Length)).ToArray());
                }
                return new LineEditor.Completion (string.Empty, Array.Empty<string>());
            };
        }

        public static void Loop()
        {
            string s;
            while ((s = LineEditor_!.Edit ("cmd> ", "")) != null)
            {
                if (s.Trim() == string.Empty)
                {
                    continue;
                }

                if (s == "exit")
                {
                    break;
                }

                try
                {
                    CommandParser.Dispatch(s);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.Message);
                }
            }
            
            Logger.Info("Bye.");
        }
    }
}
