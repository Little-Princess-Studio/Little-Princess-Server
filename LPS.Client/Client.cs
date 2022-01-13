using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LPS.Core.Debug;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Client
{
    public class Client
    {
        private readonly string ip_;
        private readonly int port_;
        private Socket? socket_;

        public static Client Create(string ip, int port)
        {
            var newClient = new Client(ip, port);
            return newClient;
        }

        public Client(string ip, int port)
        {
            ip_ = ip;
            port_ = port;
        }

        public void Start()
        {
            var ipa = IPAddress.Parse(ip_);
            var ipe = new IPEndPoint(ipa, port_);
            // todo: auto select net protocol later
            socket_ = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Logger.Debug($"Connect to gate: {ip_}:{port_}");
            socket_.Connect(ipe);

            if (!socket_.Connected)
            {
                socket_ = null;
                var e = new Exception($"Target cannot be connected.");
                Logger.Fatal(e, $"Target cannot be connected.");
                throw e;
            }
            
            Logger.Debug("Connected.");
        }

        public void Send(string message)
        {
            if (socket_ is null)
            {
                throw new Exception("Socket is null.");
            }

            string encryptedData;
            var rsa = RSA.Create();
            var pem = File.ReadAllText("./demo.pub").ToCharArray();
            rsa.ImportFromPem(pem);

            var byteData = Encoding.UTF8.GetBytes(message);
            encryptedData = Convert.ToBase64String(rsa.Encrypt(byteData, RSAEncryptionPadding.Pkcs1));

            Logger.Debug($"encryped data: {encryptedData}");
            
            var authMsg = new Authentication()
            {
                Content = message,
                Ciphertext = encryptedData,
            };

            var pkg = PackageHelper.FromProtoBuf(authMsg, 0);
            
            Logger.Debug($"{pkg.Header.Length} {pkg.Header.ID} {pkg.Header.Version} {pkg.Header.Type}");
            
            socket_.Send(pkg.ToBytes());
        }
    }
}
