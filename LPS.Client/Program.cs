using LPS.Core.Debug;
using LPS.Client.Console;
using LPS.Client.LPS.Core.Rpc;

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
            RpcProtobufDefs.Init();
            CommandParser.ScanCommands("LPS.Client");

            Client.Instance.Init("127.0.0.1", 11001);
            Client.Instance.Start();

            AutoCompleteConsole.Init();
            AutoCompleteConsole.Loop();
            
            Client.Instance.Stop();
            Client.Instance.WaitForExit();
        }
    }
}
