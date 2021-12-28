using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Ipc;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

using TcpClient = LPS.Core.Rpc.TcpClient;

namespace LPS.Core
{
    /// <summary>
    /// Each gate need maintain multiple connections from remote clients
    /// and maintain a connection to hostmanager.
    /// For hostmanager, gate is a client
    /// for remote clients, gate is a server.
    /// All the gate mailbox info will be saved in redis, and gate will
    /// repeatly sync these info from redis.
    /// </summary>
    public class Gate
    {
        public string Name { get; private set; }
        public string IP { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }
        private readonly TcpServer tcpGateServer_;
        private readonly TcpClient[] tcpClientsToServer_;

        private readonly Random rand_ = new Random();

        public Gate(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort, Tuple<string, int>[] servers)
        {
            this.Name = name;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;

            tcpGateServer_ = new(ip, port);
            tcpGateServer_.OnInit = () =>
            {
                this.RegisterServerMessageHandlers();
                tcpClientsToServer_[rand_.Next(tcpClientsToServer_.Length)].Send(
                    new CreateEntity()
                    {
                        CreateType = CreateType.Manual,
                        EntityClassName = "InnerClass",
                    }
                );
            };
            tcpGateServer_.OnDispose = this.UnregisterServerMessageHandlers;

            // connect to each server
            tcpClientsToServer_ = new TcpClient[servers.Length];
            int idx = 0;
            foreach (var (serverIP, serverPort) in servers)
            {
                tcpClientsToServer_[idx] = new TcpClient(serverIP, serverPort);
                ++idx;
            }

            //todo: connect to hostmanager
        }

        private void RegisterServerMessageHandlers()
        {
            tcpGateServer_.RegisterMessageHandler(PackageType.Authentication, this.HandleAuthentication);
            tcpGateServer_.RegisterMessageHandler(PackageType.CreateMailBox, this.HandleCreateMailBox);
            tcpGateServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        }

        private void UnregisterServerMessageHandlers()
        {
            tcpGateServer_.UnregisterMessageHandler(PackageType.Authentication, this.HandleAuthentication);
            tcpGateServer_.UnregisterMessageHandler(PackageType.CreateMailBox, this.HandleCreateMailBox);
            tcpGateServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
        }

        public void Stop()
        {
            Array.ForEach(tcpClientsToServer_, client => client.Stop());
            this.tcpGateServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start gate at {this.IP}:{this.Port}");
            this.tcpGateServer_.Run();

            Array.ForEach(tcpClientsToServer_, client => client.Run());

            // gate main thread will stuck here
            this.tcpGateServer_.WaitForExit();
        }

        private void HandleAuthentication(object arg)
        {
            (var msg, var conn) = arg as Tuple<IMessage, Connection>;

            var auth = msg as Authentication;

            // TODO: Cache the rsa object
            var rsa = RSA.Create();
            var pem = File.ReadAllText("./Config/demo.key").ToCharArray();
            rsa.ImportFromPem(pem);
            var byteData = Convert.FromBase64String(auth.Ciphertext);
            var decryptedBytes = rsa.Decrypt(byteData, RSAEncryptionPadding.Pkcs1);
            var decryptedData = Encoding.UTF8.GetString(decryptedBytes);

            Logger.Info($"Got auth req {auth.Content}, {auth.Ciphertext}");
            Logger.Info($"Got decrypted content: {decryptedData}");

            // auth succ
            if (decryptedData == auth.Content)
            {
                Logger.Info("Auth succ");
                // todo: notify server to create mailbox

                // server_mb = randomSelect(servers)
                // [var mailbox] = await RPC.Call(server_mb, "CreateUntrusted", content) as Tuple<MailBox>
                // Send("CreateMailBox")
            }
            else
            {
                Logger.Info("Auth failed");
                conn.DisConnect();
                conn.TokenSource.Cancel();
            }

        }

        private void HandleCreateMailBox(object arg)
        {

        }

        private void HandleEntityRpc(object arg)
        {

        }

    }
}
