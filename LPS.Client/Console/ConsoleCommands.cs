using System.Security.Cryptography;
using System.Text;
using LPS.Core.Debug;
using LPS.Core.Rpc.InnerMessages;

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
            const string message = "authority-content";
            
            var rsa = RSA.Create();
            var pem = File.ReadAllText("./Config/demo.pub").ToCharArray();
            rsa.ImportFromPem(pem);

            var byteData = Encoding.UTF8.GetBytes(message);
            var encryptedData = Convert.ToBase64String(rsa.Encrypt(byteData, RSAEncryptionPadding.Pkcs1));
            
            var authMsg = new Authentication
            {
                Content = message,
                Ciphertext = encryptedData,
            };
            
            Client.Instance.Send(authMsg);
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
