using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Google.Protobuf;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Ipc;
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
        private readonly TcpClient[] tcpClientsToOtherGate_;
        private readonly ConcurrentDictionary<(int, PackageType), Action<object>> tcpClientsActions_ = new();
        private readonly ConcurrentQueue<Tuple<TcpClient, IMessage, bool>> sendQueue_ = new();
        private readonly SandBox clientsSendQueueSandBox_;
        private readonly SandBox clientsPumpMsgSandBox_;

        private ServerEntity? entity_;

        private readonly Random random_ = new();

        // if all the tcpclients have connected to server/other gate, countdownEvent_ will down to 0
        private readonly CountdownEvent tcpClientConnectedCountdownEvent_;
        private readonly CountdownEvent otherGatesReadyCountdownEvent_;

        public Gate(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort,
            Tuple<string, int>[] servers, Tuple<string, int>[] otherGates)
        {
            this.Name = name;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;

            // tcp gate server handles msg from server/other gates
            tcpGateServer_ = new(ip, port)
            {
                OnInit = this.RegisterMessageFromServerAndOtherGateHandlers,
                OnDispose = this.UnregisterMessageFromServerAndOtherGateHandlers
            };

            // connect to each server
            // tcp gate to server handlers msg from server
            tcpClientsToServer_ = new TcpClient[servers.Length];
            var idx = 0;
            foreach (var (serverIP, serverPort) in servers)
            {
                var tmpIdx = idx;
                var client = new TcpClient(serverIP, serverPort, sendQueue_)
                {
                    OnInit = () => this.RegisterGateMessageHandlers(tmpIdx),
                    OnDispose = () => this.UnregisterGateMessageHandlers(tmpIdx),
                };
                tcpClientsToServer_[idx] = client;
                ++idx;
            }

            var waitCount = servers.Length + otherGates.Length;
            tcpClientConnectedCountdownEvent_ = new(waitCount);
            otherGatesReadyCountdownEvent_ = new(otherGates.Length);

            tcpClientsToServer_[0].OnInit = () =>
            {
                this.RegisterGateMessageHandlers(0);

                // var client = tcpClientsToServer_[0];
               var client = RandomServerClient();

                client.Send(
                    new CreateEntity()
                    {
                        CreateType = CreateType.Manual,
                        EntityClassName = "None",
                    },
                    false
                );
            };

            // connect to each gate
            // tcp gate to other gate only send msg to other gate's server
            tcpClientsToOtherGate_ = new TcpClient[otherGates.Length];
            idx = 0;
            foreach (var (otherGateIP, otherGatePort) in otherGates)
            {
                var tmpIdx = idx;
                var client = new TcpClient(otherGateIP, otherGatePort, sendQueue_)
                {
                    OnConnected = () => tcpClientConnectedCountdownEvent_.Signal(),
                };

                tcpClientsToOtherGate_[idx] = client;
                ++idx;
            }

            //todo: connect to hostmanager

            clientsSendQueueSandBox_ = SandBox.Create(this.SendQueueMessageHandler);
            clientsPumpMsgSandBox_ = SandBox.Create(this.PumpMessageHandler);
        }

        #region register server message

        private void RegisterMessageFromServerAndOtherGateHandlers()
        {
            tcpGateServer_.RegisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);
            tcpGateServer_.RegisterMessageHandler(PackageType.Control, this.HandleControlMessage);
            tcpGateServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromClient);
        }

        private void UnregisterMessageFromServerAndOtherGateHandlers()
        {
            tcpGateServer_.UnregisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);
            tcpGateServer_.UnregisterMessageHandler(PackageType.Control, this.HandleControlMessage);
            tcpGateServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromClient);
        }

        private void HandleEntityRpcFromClient(object arg)
        {
            // if gate's server have recieved the EntityRpc msg, it must be redirect from other gates
            Logger.Info("Handle EntityRpc From Other Gates.");

            var (msg, _, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var entityRpc = (msg as EntityRpc)!;
            this.HandleEntityRpcMessageOnGate(entityRpc);
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

        private void HandleControlMessage(object arg)
        {
            var (msg, _, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;

            var control = (msg as Control)!;

            Logger.Info($"Got control msg: {control}");

            if (control.From == RemoteType.Gate)
            {
                if (control.Message == ControlMessage.Ready)
                {
                    Logger.Info($"Got gate ready msg");
                    otherGatesReadyCountdownEvent_.Signal();
                }
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

            tcpClientConnectedCountdownEvent_.Signal();
        }

        private void HandleEntityRpcFromServer(TcpClient client, object arg)
        {
            Logger.Info("HandleEntityRpcFromServer");

            var (msg, _, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var entityRpc = (msg as EntityRpc)!;
            this.HandleEntityRpcMessageOnGate(entityRpc);
        }

        #endregion

        private void HandleEntityRpcMessageOnGate(EntityRpc entityRpc)
        {
            var targetEntityMailBox = entityRpc.EntityMailBox!;

            Logger.Debug($"rpc to {targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}" +
            $" {targetEntityMailBox.HostNum}");

            // if rpc's target is gate entity
            if (entity_!.MailBox!.CompareFull(targetEntityMailBox))
            {
                Logger.Debug("send to gate itself");
                Logger.Debug($"Call gate entity: {entityRpc.MethodName}");
                RpcHelper.CallLocalEntity(this.entity_, entityRpc);
            }
            else
            {
                // todo: dictionary cache
                var gate = this.tcpClientsToOtherGate_
                            .First(client => client.TargetIP == targetEntityMailBox.IP
                                && client.TargetPort == targetEntityMailBox.Port);

                // if rpc's target is other gate entity
                if (gate != null)
                {
                    Logger.Debug("redirect to gate's entity");
                    gate.Send(entityRpc);
                }
                else
                {
                    var serverClient = this.FindServerOfEntity(entityRpc.EntityMailBox);
                    if (serverClient != null)
                    {
                        Logger.Debug("redirect to server");
                        serverClient.Send(entityRpc);
                    }
                    else
                    {
                        Logger.Warn($"invalid rpc target mailbox: ${targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}" +
                            $"{targetEntityMailBox.HostNum}");
                    }
                }
            }
        }

        private void SendQueueMessageHandler()
        {
            while (!tcpGateServer_.Stopped)
            {
                if (!sendQueue_.IsEmpty)
                {
                    var res = sendQueue_.TryDequeue(out var tp);
                    if (res)
                    {
                        var (client, msg, reentry) = tp;

                        var id = client.GenerateMsgID();
                        // if (!reentry)
                        // {
                        //     tokenSequence_.Enqueue(id);
                        // }

                        var pkg = PackageHelper.FromProtoBuf(msg, id);

                        try
                        {
                            client.Socket!.Send(pkg.ToBytes());
                        }
                        catch (Exception e)
                        {
                            // TODO: try reconnect
                            Logger.Error(e, "Send msg failed.");
                            this.Stop();
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        private void PumpMessageHandler()
        {
            try
            {
                while (!tcpGateServer_.Stopped)
                {
                    foreach (var client in tcpClientsToServer_)
                    {
                        client.Pump();
                    }
                    
                    foreach (var client in tcpClientsToOtherGate_)
                    {
                        client.Pump();
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Pump message failed.");
            }
        }


        private TcpClient RandomServerClient()
        {
            return tcpClientsToServer_[random_.Next(tcpClientsToServer_.Length)];
        }

        public void Stop()
        {
            Array.ForEach(tcpClientsToServer_, client => client.Stop());
            Array.ForEach(tcpClientsToOtherGate_, client => client.Stop());
            this.tcpGateServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start gate at {this.IP}:{this.Port}");
            tcpGateServer_.Run();
            
            Array.ForEach(tcpClientsToServer_, client => client.Run());
            Array.ForEach(tcpClientsToOtherGate_, client => client.Run());

            clientsSendQueueSandBox_.Run();
            clientsPumpMsgSandBox_.Run();

            Logger.Debug("Wait for server's mailbox");
            tcpClientConnectedCountdownEvent_.Wait();

            // NOTE: if tcpClient hash successfully connected to remote, it means remote is already
            // ready to pump message. (tcpServer's OnInit is invoked before tcpServers' Listen)
            Logger.Debug("Wait for other gate's ready");
            Array.ForEach(tcpClientsToOtherGate_, client =>
            {
                var msg = new Control()
                {
                    From = RemoteType.Gate,
                    Message = ControlMessage.Ready,
                };

                client.Send(msg);
            });

            otherGatesReadyCountdownEvent_.Wait();
            Logger.Debug("waiting completed");

            Logger.Debug("Try to call Echo method by mailbox");
            Array.ForEach(tcpClientsToServer_, client =>
            {
                var serverEntityMailBox = client.MailBox!;
                var res = entity_!.Call(serverEntityMailBox, "Echo", "Hello");
                res.ContinueWith(t => Logger.Info($"Echo Res Callback"));
            });

            // gate main thread will stuck here
            tcpGateServer_.WaitForExit();
            clientsSendQueueSandBox_.WaitForExit();
            clientsPumpMsgSandBox_.WaitForExit();

            Logger.Debug("Gate Exit.");
        }
    }
}