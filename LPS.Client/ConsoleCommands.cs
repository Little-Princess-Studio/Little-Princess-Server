using LPS.Core.Debug;

namespace LPS.Client
{
    public static class ConsoleCommands
    {
        [ConsoleCommand("echo")]
        public static void Echo(string message)
        {
            Logger.Info($"echo: {message}");
        }
    }    
}
