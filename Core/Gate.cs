using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;
using MailBox = LPS.Core.Rpc.InnerMessages.MailBox;
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
        private readonly ConcurrentDictionary<(int, PackageType), Action<object>> tcpClientsActions_ = new();
        private ServerEntity? entity_;

        public Gate(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort,
            Tuple<string, int>[] servers)
        {
            this.Name = name;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;

            tcpGateServer_ = new(ip, port)
            {
                OnInit = this.RegisterMessageFromServerHandlers,
                OnDispose = this.UnregisterMessageFromServerHandlers
            };

            // connect to each server
            tcpClientsToServer_ = new TcpClient[servers.Length];
            var idx = 0;
            foreach (var (serverIP, serverPort) in servers)
            {
                var tmpIdx = idx;
                var client = new TcpClient(serverIP, serverPort)
                {
                    OnInit = () => this.RegisterGateMessageHandlers(tmpIdx),
                    OnDispose = () => this.UnregisterGateMessageHandlers(tmpIdx),
                };
                tcpClientsToServer_[idx] = client;
                ++idx;
            }

            tcpClientsToServer_[0].OnInit = () =>
            {
                this.RegisterGateMessageHandlers(0);
                tcpClientsToServer_[0].Send(
                    new CreateEntity()
                    {
                        CreateType = CreateType.Manual,
                        EntityClassName = "None",
                    },
                    false
                );
            };

            //todo: connect to hostmanager
        }

        #region register server message

        private void RegisterMessageFromServerHandlers()
        {
            tcpGateServer_.RegisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);
        }

        private void UnregisterMessageFromServerHandlers()
        {
            tcpGateServer_.UnregisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);
        }

        private void HandleAuthenticationFromClient(object arg)
        {
            var (msg, conn, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;

            var auth = (msg as Authentication)!;

            // TODO: Cache the rsa object
            var decryptedData = DecryptedCiphertext(auth);

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

        private static string DecryptedCiphertext(Authentication auth)
        {
            var rsa = RSA.Create();
            var pem = File.ReadAllText("./Config/demo.key").ToCharArray();
            rsa.ImportFromPem(pem);
            var byteData = Convert.FromBase64String(auth.Ciphertext);
            var decryptedBytes = rsa.Decrypt(byteData, RSAEncryptionPadding.Pkcs1);
            var decryptedData = Encoding.UTF8.GetString(decryptedBytes);
            return decryptedData;
        }

        #endregion

        #region register client message

        private void RegisterGateMessageHandlers(int idx)
        {
            var client = tcpClientsToServer_[idx];

            var createMailBoxHandler = (object arg) => this.HandleCreateEntityResFromServer(client, arg);
            tcpClientsActions_[(idx, PackageType.CreateEntityRes)] = createMailBoxHandler;

            var requireMailBoxHandler = (object arg) => this.HandleExchangeMailBoxResFromServer(client, arg);
            tcpClientsActions_[(idx, PackageType.ExchangeMailBoxRes)] = requireMailBoxHandler;

            var entityRpcHandler = (object arg) => this.HandleEntityRpcFromServer(client, arg);
            tcpClientsActions_[(idx, PackageType.EntityRpc)] = entityRpcHandler;

            client.RegisterMessageHandler(PackageType.CreateEntityRes, createMailBoxHandler);
            client.RegisterMessageHandler(PackageType.ExchangeMailBoxRes, requireMailBoxHandler);
            client.RegisterMessageHandler(PackageType.EntityRpc, entityRpcHandler);
        }

        private void UnregisterGateMessageHandlers(int idx)
        {
            var client = tcpClientsToServer_[idx];
            client.UnregisterMessageHandler(
                PackageType.CreateEntityRes,
                tcpClientsActions_[(idx, PackageType.CreateEntityRes)]);

            client.UnregisterMessageHandler(
                PackageType.ExchangeMailBoxRes,
                tcpClientsActions_[(idx, PackageType.ExchangeMailBoxRes)]);

            client.UnregisterMessageHandler(
                PackageType.EntityRpc,
                tcpClientsActions_[(idx, PackageType.EntityRpc)]);
        }

        private void HandleCreateEntityResFromServer(TcpClient client, object arg)
        {
            var (msg, _, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var createEntityRes = (msg as CreateEntityRes)!.Mailbox;

            Logger.Info(
                $"mailbox {createEntityRes.ID} {createEntityRes.IP} {createEntityRes.Port} {createEntityRes.HostNum}");

            var serverEntityMailBox = new Rpc.MailBox(createEntityRes.ID, this.IP, this.Port, this.HostNum);
            entity_ = new ServerEntity(serverEntityMailBox, entityRpc =>
            {
                var targetMailBox = entityRpc.EntityMailBox;
                var clientToServer = FindServerOfEntity(targetMailBox);
                clientToServer.Send(entityRpc);
            });

            Logger.Info($"gate entity create succ");

            var exchangeMailBox = new ExchangeMailBox()
            {
                Mailbox = RpcHelper.RpcMailBoxToPbMailBox(serverEntityMailBox),
            };
            Array.ForEach(tcpClientsToServer_, c => c.Send(exchangeMailBox));
        }

        private TcpClient FindServerOfEntity(MailBox targetMailBox)
        {
            var clientToServer = this.tcpClientsToServer_
                .First(clientToServer => clientToServer.MailBox!.IP == targetMailBox.IP
                                         && clientToServer.MailBox!.Port == targetMailBox.Port
                                         && clientToServer.MailBox!.HostNum == targetMailBox.HostNum);
            return clientToServer;
        }

        private void HandleExchangeMailBoxResFromServer(TcpClient client, object arg)
        {
            var (msg, conn, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var serverMailBox = (msg as ExchangeMailBoxRes)!.Mailbox;
            var serverEntityMailBox = RpcHelper.PbMailBoxToRpcMailBox(serverMailBox);

            conn.MailBox = serverEntityMailBox;
            client.MailBox = serverEntityMailBox;

            Logger.Info($"got server mailbox: {serverEntityMailBox}");
            Logger.Debug("Try to call Echo method by mailbox");

            var res = entity_!.Call(serverEntityMailBox, "Echo", "Hello");
            res.ContinueWith(t => Logger.Info($"Echo Res Callback"));
        }

        private void HandleEntityRpcFromServer(TcpClient client, object arg)
        {
            Logger.Info("HandleEntityRpcFromServer");
            var (msg, conn, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var entityRpc = (msg as EntityRpc)!;

            var targetEntityMailBox = entityRpc.EntityMailBox!;

            Logger.Info($"{targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}"+ 
            $"{targetEntityMailBox.HostNum}");
            Logger.Info($"{entity_.MailBox.IP} {entity_.MailBox.Port} {entity_.MailBox.ID}"+ 
                        $"{entity_.MailBox.HostNum}");
            
            if (targetEntityMailBox.IP == entity_!.MailBox!.IP &&
                targetEntityMailBox.Port == entity_.MailBox.Port &&
                targetEntityMailBox.ID == entity_.MailBox.ID &&
                targetEntityMailBox.HostNum == entity_.MailBox.HostNum)
            {
                Logger.Debug($"Call gate entity: {entityRpc.MethodName}");
                RpcHelper.CallLocalEntity(this.entity_, entityRpc);
            }
            else
            {
                // var serverClient = this.FindServerOfEntity(entityRpc.EntityMailBox);
                // serverClient.Send(entityRpc);
            }
        }

        #endregion

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
    }
}