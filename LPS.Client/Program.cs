using LPS.Core.Debug;
using LPS.Client.Console;

namespace LPS.Client
{
    public class Program
    {
        private static Random random = new Random();

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        
        public static void Main(string[] args)
        {
            Logger.Init("client");
            CommandParser.ScanCommands("LPS.Client");

            // Client.Instance.Init("52.175.74.209", 11001);
            // Client.Instance.Start();

            AutoCompleteConsole.Init();
            AutoCompleteConsole.Loop();
            
            // Client.Instance.Stop();
            // Client.Instance.WaitForExit();
        }
    }
}
