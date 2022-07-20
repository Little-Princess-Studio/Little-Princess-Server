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
        
        [ConsoleCommand("send.echo")]
        public static async void Echo()
        {
            var startTime = new TimeSpan(System.DateTime.Now.Ticks);
            for (int i = 0; i < 1; ++i)
            {
                var start = new TimeSpan(System.DateTime.Now.Ticks);
                var res = await ClientGlobal.ShadowClientEntity
                    .Server
                    .Call<string>("Echo", $"Hello, LPS, times {i}");
                
                var end = new TimeSpan(System.DateTime.Now.Ticks);
                
                Logger.Debug($"call res {res}, latancy: {(end - start).TotalMilliseconds} ms");
                
                Thread.Sleep(50);
            }
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

        [ConsoleCommand("send.transfer")]
        public static async void Transfer(string id, string ip, int port, int hostNum)
        {
            var cellMailBox = new Core.Rpc.MailBox(id, ip, port, hostNum);

            ClientGlobal.ShadowClientEntity.Server.Notify(
                "TransferIntoCell",
                cellMailBox,
                ""
            );
        }
    }
}

