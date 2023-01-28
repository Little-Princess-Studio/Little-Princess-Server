using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Google.Protobuf;
using LPS.Common.Core.Debug;
using LPS.Common.Core.Ipc;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Server.Core.Entity;
using LPS.Server.Core.Rpc;
using LPS.Server.Core.Rpc.InnerMessages;
using MailBox = LPS.Common.Core.Rpc.InnerMessages.MailBox;

namespace LPS.Server.Core
{
    /// <summary>
    /// Each gate need maintain multiple connections from remote clients
    /// and maintain a connection to hostmanager.
    /// For hostmanager, gate is a client
    /// for remote clients, gate is a server.
    /// All the gate mailbox info will be saved in redis, and gate will
    /// repeatly sync these info from redis.
    /// </summary>
    public class Gate : IInstance
    {
        public string Name { get; private set; }
        public string Ip { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        public InstanceType InstanceType => InstanceType.Gate;

        private readonly ConcurrentDictionary<(int, PackageType), Action<object>> tcpClientsActions_ = new();

        private readonly ConcurrentQueue<(Rpc.TcpClient Client, IMessage Message, bool IsReentry)> sendQueue_ = new();

        // private readonly SandBox clientsSendQueueSandBox_;
        private readonly ConcurrentDictionary<uint, Connection> createEntityMapping_ = new();
        private readonly Dictionary<string, (Common.Core.Rpc.MailBox, Connection)> entityIdToClientConnMapping_ = new();

        private ServerEntity? entity_;

        private readonly Random random_ = new();

        // if all the tcpclients have connected to server/other gate, countdownEvent_ will down to 0
        // private readonly CountdownEvent tcpClientConnectedCountdownEvent_;
        // private readonly CountdownEvent otherGatesReadyCountdownEvent_;
        // private readonly CountdownEvent serverMailboxGotEvent_;

        private readonly CountdownEvent hostManagerConnectedEvent_;
        private readonly CountdownEvent localEntityGeneratedEvent_;

        private uint createEntityCounter_;

        private readonly SandBox clientsPumpMsgSandBox_;

        private readonly TcpServer tcpGateServer_;
        private readonly TcpClient[] tcpClientsToServer_;
        private readonly TcpClient[] tcpClientsToOtherGate_;
        private readonly TcpClient clientToHostManager_;


        public Gate(string name, string ip, int port, int hostNum, string hostManagerIp, int hostManagerPort,
            (string IP, int Port)[] servers, (string InnerIp, string Ip, int Port)[] otherGates)
        {
            this.Name = name;
            this.Ip = ip;
            this.Port = port;
            this.HostNum = hostNum;

            // tcp gate server handles msg from server/other gates
            tcpGateServer_ = new TcpServer(ip, port)
            {
                OnInit = this.RegisterMessageFromServerAndOtherGateHandlers,
                OnDispose = this.UnregisterMessageFromServerAndOtherGateHandlers
            };

            // var waitCount = servers.Length + otherGates.Length;
            // tcpClientConnectedCountdownEvent_ = new(waitCount);
            // otherGatesReadyCountdownEvent_ = new(otherGates.Length);
            // serverMailboxGotEvent_ = new(servers.Length);

            // connect to each server
            // tcp gate to server handlers msg from server
            // tcpClientsToServer_ = new TcpClient[servers.Length];
            // var idx = 0;
            // foreach (var (serverIp, serverPort) in servers)
            // {
            //     var tmpIdx = idx;
            //     Logger.Debug($"server ip: {serverIp} server port: {serverPort}");
            //     var client = new TcpClient(serverIp, serverPort, sendQueue_)
            //     {
            //         OnInit = () => this.RegisterGateMessageHandlers(tmpIdx),
            //         OnDispose = () => this.UnregisterGateMessageHandlers(tmpIdx),
            //         OnConnected = () => tcpClientConnectedCountdownEvent_.Signal(),
            //     };
            //     tcpClientsToServer_[idx] = client;
            //     ++idx;
            // }

            // connect to each gate
            // tcp gate to other gate only send msg to other gate's server
            // tcpClientsToOtherGate_ = new Rpc.TcpClient[otherGates.Length];
            // idx = 0;
            // foreach (var (otherGateInnerIp, otherGateIp, otherGatePort) in otherGates)
            // {
            //     var tmpIdx = idx;
            //     var client = new TcpClient(otherGateInnerIp, otherGatePort, sendQueue_)
            //     {
            //         OnConnected = () => tcpClientConnectedCountdownEvent_.Signal(),
            //         TargetIp = otherGateIp
            //     };
            //
            //     tcpClientsToOtherGate_[idx] = client;
            //     ++idx;
            // }

            hostManagerConnectedEvent_ = new CountdownEvent(1);
            localEntityGeneratedEvent_ = new CountdownEvent(1);
            clientToHostManager_ = new TcpClient(hostManagerIp, hostManagerPort,
                new ConcurrentQueue<(TcpClient, IMessage, bool)>())
            {
                OnInit = () =>
                {
                    var client = clientToHostManager_!;
                    client.Send(
                        new RequireCreateEntity
                        {
                            EntityType = EntityType.GateEntity,
                            CreateType = CreateType.Manual,
                            EntityClassName = "",
                            Description = "",
                            ConnectionID = createEntityCounter_++
                        },
                        false
                    );
                },
                OnConnected = () => hostManagerConnectedEvent_.Signal(1)
            };

            clientsPumpMsgSandBox_ = SandBox.Create(this.PumpMessageHandler);
        }

        #region register server message

        private void RegisterMessageFromServerAndOtherGateHandlers()
        {
            tcpGateServer_.RegisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);
            // tcpGateServer_.RegisterMessageHandler(PackageType.Control, this.HandleControlMessage);
            tcpGateServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromClient);
            tcpGateServer_.RegisterMessageHandler(PackageType.RequirePropertyFullSync,
                this.HandleRequireFullSyncFromClient);
            tcpGateServer_.RegisterMessageHandler(PackageType.PropertyFullSyncAck,
                this.HandlePropertyFullSyncAckFromClient);

            clientToHostManager_.RegisterMessageHandler(PackageType.RequireCreateEntityRes,
                this.HandleRequireCreateEntityResFromHost);
        }

        private void UnregisterMessageFromServerAndOtherGateHandlers()
        {
            tcpGateServer_.UnregisterMessageHandler(PackageType.Authentication, this.HandleAuthenticationFromClient);
            // tcpGateServer_.UnregisterMessageHandler(PackageType.Control, this.HandleControlMessage);
            tcpGateServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpcFromClient);
            tcpGateServer_.UnregisterMessageHandler(PackageType.RequirePropertyFullSync,
                this.HandleRequireFullSyncFromClient);
            tcpGateServer_.UnregisterMessageHandler(PackageType.PropertyFullSyncAck,
                this.HandlePropertyFullSyncAckFromClient);
            
            clientToHostManager_.UnregisterMessageHandler(PackageType.RequireCreateEntityRes,
                this.HandleRequireCreateEntityResFromHost);
        }

        private void HandleEntityRpcFromClient(object arg)
        {
            // if gate's server have recieved the EntityRpc msg, it must be redirect from other gates
            Logger.Info("Handle EntityRpc From Other Gates.");

            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg;
            var entityRpc = (msg as EntityRpc)!;
            this.HandleEntityRpcMessageOnGate(entityRpc);
        }

        private void HandleAuthenticationFromClient(object arg)
        {
            var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg;
            var auth = (msg as Authentication)!;

            // TODO: Cache the rsa object
            var decryptedData = DecryptedCiphertext(auth);

            Logger.Info($"Got decrypted content: {decryptedData}");

            if (decryptedData == auth.Content)
            {
                Logger.Info("Auth success");

                var connId = createEntityCounter_++;

                var createEntityMsg = new RequireCreateEntity
                {
                    EntityClassName = "Untrusted",
                    CreateType = CreateType.Anywhere,
                    Description = "",
                    EntityType = EntityType.ServerClientEntity,
                    ConnectionID = connId
                };

                if (conn.ConnectionID != uint.MaxValue)
                {
                    throw new Exception("Entity is creating");
                }

                conn.ConnectionID = connId;
                createEntityMapping_[connId] = conn;

                var serverClient = RandomServerClient();
                serverClient.Send(createEntityMsg, false);
            }
            else
            {
                Logger.Warn("Auth failed");
                conn.DisConnect();
                conn.TokenSource.Cancel();
            }
        }

        // private void HandleControlMessage(object arg)
        // {
        //     var (msg, _, _) = ((IMessage, Connection, UInt32)) arg;
        //
        //     var control = (msg as Control)!;
        //
        //     Logger.Info($"Got control msg: {control}");
        //
        //     if (control.From == RemoteType.Gate)
        //     {
        //         if (control.Message == ControlMessage.Ready)
        //         {
        //             Logger.Info("Got gate ready msg");
        //             otherGatesReadyCountdownEvent_.Signal();
        //         }
        //     }
        // }

        private void HandleRequireFullSyncFromClient(object? arg)
        {
            Logger.Info("HandleRequireFullSyncFromClient");
            var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg!;
            var requirePropertyFullSyncMsg = (msg as RequirePropertyFullSync)!;

            this.RedirectMsgToEntityOnServer(requirePropertyFullSyncMsg.EntityId, msg);
        }

        private void HandlePropertyFullSyncAckFromClient(object? arg)
        {
            Logger.Info("HandlePropertyFullSyncAck");
            var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg!;
            var propertyFullSyncAck = (msg as PropertyFullSyncAck)!;

            this.RedirectMsgToEntityOnServer(propertyFullSyncAck.EntityId, msg);
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

        private void RegisterGateMessageHandlers(int serverIdx)
        {
            var client = tcpClientsToServer_[serverIdx];

            // var createMailBoxResHandler = (object arg) => this.HandleRequireCreateEntityResFromServer(client, arg);
            // tcpClientsActions_[(serverIdx, PackageType.RequireCreateEntityRes)] = createMailBoxResHandler;

            // var exchangeMailBoxResHandler = (object arg) => this.HandleExchangeMailBoxResFromServer(client, arg);
            // tcpClientsActions_[(serverIdx, PackageType.ExchangeMailBoxRes)] = exchangeMailBoxResHandler;

            var entityRpcHandler = (object arg) => this.HandleEntityRpcFromServer(client, arg);
            tcpClientsActions_[(serverIdx, PackageType.EntityRpc)] = entityRpcHandler;

            var propertyFullSync = (object arg) => this.HandlePropertyFullSyncFromServer(client, arg);
            tcpClientsActions_[(serverIdx, PackageType.PropertyFullSync)] = propertyFullSync;

            var propSyncCommandList = (object arg) => this.HandlePropertySyncCommandListFromServer(client, arg);
            tcpClientsActions_[(serverIdx, PackageType.PropertySyncCommandList)] = propSyncCommandList;

            // client.RegisterMessageHandler(PackageType.RequireCreateEntityRes, createMailBoxResHandler);
            // client.RegisterMessageHandler(PackageType.ExchangeMailBoxRes, exchangeMailBoxResHandler);
            client.RegisterMessageHandler(PackageType.EntityRpc, entityRpcHandler);
            client.RegisterMessageHandler(PackageType.PropertyFullSync, propertyFullSync);
            client.RegisterMessageHandler(PackageType.PropertySyncCommandList, propSyncCommandList);

            Logger.Info($"client {serverIdx} registered msg");
        }

        private void UnregisterGateMessageHandlers(int idx)
        {
            var client = tcpClientsToServer_[idx];
            // client.UnregisterMessageHandler(
            //     PackageType.RequireCreateEntityRes,
            //     tcpClientsActions_[(idx, PackageType.RequireCreateEntityRes)]);

            // client.UnregisterMessageHandler(
            //     PackageType.ExchangeMailBoxRes,
            //     tcpClientsActions_[(idx, PackageType.ExchangeMailBoxRes)]);

            client.UnregisterMessageHandler(
                PackageType.EntityRpc,
                tcpClientsActions_[(idx, PackageType.EntityRpc)]);

            client.UnregisterMessageHandler(
                PackageType.PropertyFullSync,
                tcpClientsActions_[(idx, PackageType.PropertyFullSync)]);

            client.UnregisterMessageHandler(
                PackageType.PropertySyncCommandList,
                tcpClientsActions_[(idx, PackageType.PropertySyncCommandList)]);
        }

        private void HandlePropertySyncCommandListFromServer(TcpClient _, object arg)
        {
            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg;
            var propertySyncCommandList = (msg as PropertySyncCommandList)!;

            Logger.Info($"property sync: {propertySyncCommandList.Path}" +
                        $" {propertySyncCommandList.EntityId}" +
                        $" {propertySyncCommandList.PropType}");

            // TODO: Redirect to shadow entity on server
            this.RedirectMsgToEntityOnClient(propertySyncCommandList.EntityId, propertySyncCommandList);
        }

        private void HandleRequireCreateEntityResFromHost(object arg)
        {
            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg;
            var createEntityRes = (msg as RequireCreateEntityRes)!;

            Logger.Info("Create gate entity success.");
            var serverEntityMailBox =
                new Common.Core.Rpc.MailBox(createEntityRes.Mailbox.ID, this.Ip, this.Port, this.HostNum);
            entity_ = new(serverEntityMailBox)
            {
                OnSend = entityRpc =>
                {
                    var targetMailBox = entityRpc.EntityMailBox;
                    var clientToServer = FindServerOfEntity(targetMailBox);
                    if (clientToServer != null)
                    {
                        clientToServer.Send(entityRpc);
                    }
                    else
                    {
                        throw new Exception($"gate's server client not found: {targetMailBox}");
                    }
                }
            };

            localEntityGeneratedEvent_.Signal(1);
            // var exchangeMailBox = new ExchangeMailBox
            // {
            //     Mailbox = RpcHelper.RpcMailBoxToPbMailBox(serverEntityMailBox)
            // };
            // Array.ForEach(tcpClientsToServer_, c => c.Send(exchangeMailBox));
        }

        private void HandleRequireCreateEntityResFromServer(TcpClient _, object arg)
        {
            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg;
            var createEntityRes = (msg as RequireCreateEntityRes)!;

            Logger.Info($"create entity res {createEntityRes.EntityType}");

            if (createEntityRes.EntityType == EntityType.ServerClientEntity)
            {
                Logger.Info("Create server client entity success.");
                var serverClientEntity = RpcHelper.PbMailBoxToRpcMailBox(createEntityRes.Mailbox);
                createEntityMapping_.Remove(createEntityRes.ConnectionID, out var conn);
                if (conn != null)
                {
                    Logger.Info($"{serverClientEntity.Id} => {conn.MailBox}");
                    entityIdToClientConnMapping_[serverClientEntity.Id] = (serverClientEntity, conn);
                    var clientCreateEntity = new ClientCreateEntity
                    {
                        EntityClassName = createEntityRes.EntityClassName,
                        ServerClientMailBox = createEntityRes.Mailbox
                    };
                    var pkg = PackageHelper.FromProtoBuf(clientCreateEntity, 0);
                    conn.Socket.Send(pkg.ToBytes());
                }
                else
                {
                    throw new Exception("Error when creating server client entity: connection id not found.");
                }
            }
        }

        private TcpClient? FindServerOfEntity(MailBox targetMailBox)
        {
            var clientToServer = tcpClientsToServer_
                .FirstOrDefault(clientToServer => clientToServer?.MailBox.Ip == targetMailBox.IP
                                                  && clientToServer.MailBox.Port == targetMailBox.Port
                                                  && clientToServer.MailBox.HostNum == targetMailBox.HostNum, null);
            return clientToServer;
        }

        private TcpClient? FindServerOfEntity(Common.Core.Rpc.MailBox targetMailBox)
        {
            var clientToServer = tcpClientsToServer_
                .FirstOrDefault(clientToServer => clientToServer?.MailBox.Ip == targetMailBox.Ip
                                                  && clientToServer.MailBox.Port == targetMailBox.Port
                                                  && clientToServer.MailBox.HostNum == targetMailBox.HostNum, null);
            return clientToServer;
        }

        // private void HandleExchangeMailBoxResFromServer(Rpc.TcpClient client, object arg)
        // {
        //     var (msg, conn, _) = ((IMessage, Connection, UInt32)) arg;
        //     var serverMailBox = (msg as ExchangeMailBoxRes)!.Mailbox;
        //     var serverEntityMailBox = RpcHelper.PbMailBoxToRpcMailBox(serverMailBox);
        //
        //     conn.MailBox = serverEntityMailBox;
        //     client.MailBox = serverEntityMailBox;
        //
        //     // serverMailboxGotEvent_.Signal();
        //
        //     Logger.Info($"got server mailbox: {serverEntityMailBox}");
        // }

        private void HandleEntityRpcFromServer(TcpClient client, object arg)
        {
            Logger.Info("HandleEntityRpcFromServer");

            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg!;
            var entityRpc = (msg as EntityRpc)!;
            this.HandleEntityRpcMessageOnGate(entityRpc);
        }

        private void HandlePropertyFullSyncFromServer(TcpClient client, object arg)
        {
            Logger.Info("HandlePropertyFullSyncFromServer");

            var (msg, _, _) = ((IMessage, Connection, UInt32)) arg!;
            var fullSync = (msg as PropertyFullSync)!;

            Logger.Info("send fullSync to client");
            this.RedirectMsgToEntityOnClient(fullSync.EntityId, msg);
        }

        #endregion

        private void RedirectMsgToEntityOnServer(string entityId, IMessage msg)
        {
            if (!entityIdToClientConnMapping_.ContainsKey(entityId))
            {
                Logger.Warn($"{entityId} not exist!");
                return;
            }

            var mb = entityIdToClientConnMapping_[entityId].Item1;
            var clientToServer = FindServerOfEntity(mb);

            if (clientToServer != null)
            {
                clientToServer.Send(msg, false);
            }
            else
            {
                Logger.Warn($"gate's server client not found: {entityId}");
            }
        }

        private void RedirectMsgToEntityOnClient(string entityId, IMessage msg)
        {
            if (!entityIdToClientConnMapping_.ContainsKey(entityId))
            {
                Logger.Warn($"{entityId} not exist!");
                return;
            }

            var conn = entityIdToClientConnMapping_[entityId].Item2;
            var pkg = PackageHelper.FromProtoBuf(msg, 0);
            conn.Socket.Send(pkg.ToBytes());
        }

        private void HandleEntityRpcMessageOnGate(EntityRpc entityRpc)
        {
            var targetEntityMailBox = entityRpc.EntityMailBox!;

            Logger.Debug($"rpc to {targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}" +
                         $" {targetEntityMailBox.HostNum}");

            // if rpc's target is gate entity
            if (entity_!.MailBox.CompareFull(targetEntityMailBox))
            {
                Logger.Debug("send to gate itself");
                Logger.Debug($"Call gate entity: {entityRpc.MethodName}");
                RpcHelper.CallLocalEntity(entity_, entityRpc);
            }
            else
            {
                var rpcType = entityRpc.RpcType;
                if (rpcType == RpcType.ClientToServer || rpcType == RpcType.ServerInside)
                {
                    // todo: dictionary cache
                    var gate = this.tcpClientsToOtherGate_
                        .FirstOrDefault(client => client!.TargetIp == targetEntityMailBox.IP
                                                  && client.TargetPort == targetEntityMailBox.Port, null);

                    // if rpc's target is other gate entity
                    if (gate != null)
                    {
                        Logger.Debug("redirect to gate's entity");
                        gate.Send(entityRpc);
                    }
                    else
                    {
                        var serverClient = this.FindServerOfEntity(targetEntityMailBox);
                        if (serverClient != null)
                        {
                            Logger.Debug($"redirect to server {serverClient.MailBox}");
                            serverClient.Send(entityRpc);
                        }
                        else
                        {
                            Logger.Warn(
                                $"invalid rpc target mailbox: {targetEntityMailBox.IP} {targetEntityMailBox.Port} {targetEntityMailBox.ID}" +
                                $"{targetEntityMailBox.HostNum}");
                        }
                    }
                }
                else if (rpcType == RpcType.ServerToClient)
                {
                    // send to client
                    Logger.Info("send rpc to client");
                    this.RedirectMsgToEntityOnClient(entityRpc.EntityMailBox.ID, entityRpc);
                }
                else
                {
                    throw new Exception($"Invalid rpc type: {rpcType}");
                }
            }
        }

        private void PumpMessageHandler()
        {
            try
            {
                while (!tcpGateServer_.Stopped)
                {
                    // foreach (var client in tcpClientsToServer_)
                    // {
                    //     client.Pump();
                    // }
                    //
                    // foreach (var client in tcpClientsToOtherGate_)
                    // {
                    //     client.Pump();
                    // }
                    
                    clientToHostManager_.Pump();

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
            clientToHostManager_.Stop();
            tcpGateServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start gate at {this.Ip}:{this.Port}");
            tcpGateServer_.Run();
            clientToHostManager_.Run();
            clientsPumpMsgSandBox_.Run();

            hostManagerConnectedEvent_.Wait();
            Logger.Debug("Host manager connected.");

            // Array.ForEach(tcpClientsToServer_, client => client.Run());
            // Array.ForEach(tcpClientsToOtherGate_, client => client.Run());

            localEntityGeneratedEvent_.Wait();
            Logger.Debug($"Gate entity created. {entity_!.MailBox}");
            // tcpClientConnectedCountdownEvent_.Wait();

            // Logger.Debug("Wait for server's mailbox");
            // serverMailboxGotEvent_.Wait();

            // NOTE: if tcpClient hash successfully connected to remote, it means remote is already
            // ready to pump message. (tcpServer's OnInit is invoked before tcpServers' Listen)
            Logger.Debug("Wait for other gate's ready");

            // Array.ForEach(tcpClientsToOtherGate_, client =>
            // {
            //     var msg = new Control()
            //     {
            //         From = RemoteType.Gate,
            //         Message = ControlMessage.Ready,
            //     };
            //
            //     client.Send(msg);
            // });

            // var msg = new Control()
            // {
            //     From = RemoteType.Gate,
            //     Message = ControlMessage.Ready,
            // };
            // clientToHostManager_.Send(msg);

            // otherGatesReadyCountdownEvent_.Wait();
            Logger.Debug("Waiting completed");

            // Logger.Debug("Try to call Echo method by mailbox");
            // Array.ForEach(tcpClientsToServer_, client =>
            // {
            //     var serverEntityMailBox = client.MailBox!;
            //     var res = entity_!.Call(serverEntityMailBox, "Echo", "Hello");
            //     res.ContinueWith(t => Logger.Info($"Echo Res Callback"));
            // });

            // gate main thread will stuck here
            // Array.ForEach(tcpClientsToOtherGate_, client => client.WaitForExit());
            // Array.ForEach(tcpClientsToServer_, client => client.WaitForExit());
            clientToHostManager_.WaitForExit();
            tcpGateServer_.WaitForExit();
            clientsPumpMsgSandBox_.WaitForExit();

            Logger.Debug("Gate Exit.");
        }
    }
}