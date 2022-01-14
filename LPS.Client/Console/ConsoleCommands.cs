using LPS.Core.Debug;

namespace LPS.Client.Console
{
    public static class ConsoleCommands
    {
        [ConsoleCommand("echo")]
        public static void Echo(string message)
        {
            Logger.Info($"echo: {message}");
        }

        [ConsoleCommand("send.authority")]
        public static void SendAuthority()
        {
            
        }
        
        [ConsoleCommand("send.ping")]
        public static void Ping()
        {
            
        }

        [ConsoleCommand("help")]
        public static void Help()
        {
            var (_, cmdDetails) = CommandParser.FindSuggestions("");

            int cnt = cmdDetails.Length;
            for (int i = 0; i < cnt; i++)
            {
                System.Console.WriteLine($"{string.Join(',', cmdDetails[i])}");
            }
        }
    }
}
