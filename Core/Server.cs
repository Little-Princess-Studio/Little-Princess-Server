using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using LPS.Core.Database;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;


/*
 * Each server instance has connections to every gates, rpc message from server's entity will ben sent to gate and
 * redirect to target server instance. 
 */
namespace LPS.Core
{
    public class Server
    {
        public string Name { get; private set; }
        public string IP { get; private set; }
        public int Port { get; private set; }
        public int HostNum { get; private set; }

        private readonly string hostManagerIP_;
        private readonly int hostManagerPort_;
        private ServerEntity? entity_;
        private readonly Dictionary<string, DistributeEntity> localEntityDict_ = new();
        private readonly TcpServer tcpServer_;
        private Connection[] GateConnections => tcpServer_.AllConnections;
        private static readonly Random Random = new Random();


        public Server(string name, string ip, int port, int hostnum, string hostManagerIP, int hostManagerPort)
        {
            this.Name = name;
            this.IP = ip;
            this.Port = port;
            this.HostNum = hostnum;

            tcpServer_ = new(ip, port)
            {
                OnInit = this.RegisterServerMessageHandlers,
                OnDispose = this.UnregisterServerMessageHandlers
            };

            hostManagerIP_ = hostManagerIP;
            hostManagerPort_ = hostManagerPort;

            // how server entity send msg
            DbHelper.GenerateNewGlobalId().ContinueWith(task =>
            {
                var newID = task.Result;
                entity_ = new(
                    new(newID, ip, port, hostnum),
                    (entityClassName, jsonDesc, mailBox) =>
                    {
                        var entity = RpcHelper.CreateEntityLocally(entityClassName, jsonDesc);
                        entity.MailBox = mailBox;
                        localEntityDict_[mailBox.ID] = entity;
                        Logger.Info($"Server register entity with mailbox {mailBox}");
                    })
                    {
                        // todo: insert local rpc call operation to pump queue, instead of directly calling local entity rpc here.
                        OnSend = entityRpc =>
                        {
                                // send this rpc to gate
                            var targetMailBox = entityRpc.EntityMailBox;

                            // send to self
                            if (targetMailBox.ID == this.entity_!.MailBox!.ID
                                && targetMailBox.IP == this.entity_.MailBox.IP
                                && targetMailBox.Port == this.entity_.MailBox.Port
                                && targetMailBox.HostNum == this.entity_.MailBox.HostNum)
                            {
                                try
                                {
                                    RpcHelper.CallLocalEntity(this.entity_, entityRpc);
                                }
                                catch(Exception e)
                                {
                                    Logger.Error(e, "Exception happend when call server entity");
                                }
                            }
                            // send to local entity
                            else if (localEntityDict_.ContainsKey(entityRpc.EntityMailBox.ID))
                            {
                                var entity = localEntityDict_[entityRpc.EntityMailBox.ID];

                                try
                                {
                                    RpcHelper.CallLocalEntity(entity, entityRpc);
                                }
                                catch(Exception e)
                                {
                                    Logger.Error(e, "Exception happend when call local entity");
                                }
                            }
                            else
                            {
                                // redirect to gate
                                this.tcpServer_.Send(entityRpc, GateConnections[0]);
                            }
                        },
                    };
            });
        }

        public void Stop()
        {
            this.tcpServer_.Stop();
        }

        public void Loop()
        {
            Logger.Debug($"Start gate at {this.IP}:{this.Port}");
            this.tcpServer_.Run();

            // gate main thread will stuck here
            this.tcpServer_.WaitForExit();
        }

        private void RegisterServerMessageHandlers()
        {
            tcpServer_.RegisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.RegisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
            tcpServer_.RegisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
        }

        private void UnregisterServerMessageHandlers()
        {
            tcpServer_.UnregisterMessageHandler(PackageType.EntityRpc, this.HandleEntityRpc);
            tcpServer_.UnregisterMessageHandler(PackageType.CreateEntity, this.HandleCreateEntity);
            tcpServer_.UnregisterMessageHandler(PackageType.ExchangeMailBox, this.HandleExchangeMailBox);
        }

        // how server handle entity rpc
        private void HandleEntityRpc(object arg)
        {
            var (msg, _, _) = (arg as Tuple<IMessage, Connection, UInt32>)!;
            var entityRpc = (msg as EntityRpc)!;

            var targetMailBox = entityRpc.EntityMailBox;

            Logger.Info($"HandleEntityRpc.entityRpc.rpcID = {entityRpc.RpcID}");

            if (this.entity_!.MailBox!.CompareFull(targetMailBox))
            {
                Logger.Debug($"Call server entity: {entityRpc.MethodName}");
                try
                {
                    RpcHelper.CallLocalEntity(this.entity_, entityRpc);
                }
                catch(Exception e)
                {
                    Logger.Error(e, "Exception happend when call server entity");
                }

            }
            else if (localEntityDict_.ContainsKey(this.entity_.MailBox.ID))
            {
                var entity = localEntityDict_[this.entity_.MailBox.ID];
                try
                {
                    RpcHelper.CallLocalEntity(entity, entityRpc);
                }
                catch(Exception e)
                {
                    Logger.Error(e, "Exception happend when call server entity");
                }
            }
            else
            {
                // redirect to gate
                this.tcpServer_.Send(entityRpc, GateConnections[0]);
            }
        }

        // private static string RandomString(int length)
        // {
        //     const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        //     return new string(Enumerable.Repeat(chars, length)
        //         .Select(s => s[Random.Next(s.Length)]).ToArray());
        // }

        private void HandleCreateEntity(object arg)
        {
            var (msg, conn, id) = (arg as Tuple<IMessage, Connection, UInt32>)!;

            var createEntity = (msg as CreateEntity)!;
            var socket = conn.Socket;

            Logger.Info($"create entity: {createEntity.CreateType}, {createEntity.EntityClassName}");

            // todo: move this quest to hostmanager
            switch (createEntity.CreateType)
            {
                case CreateType.Local:
                    break;
                case CreateType.Anywhere:
                    break;
                case CreateType.Manual:
                    DbHelper.GenerateNewGlobalId().ContinueWith(task =>
                    {
                        var newID = task.Result;
                        var entityMailBox = new CreateEntityRes
                        {
                            Mailbox = new Rpc.InnerMessages.MailBox()
                            {
                                IP = "",
                                Port = 0,
                                HostNum = (uint)this.HostNum,
                                ID = newID,
                            }
                        };
                        var pkg = PackageHelper.FromProtoBuf(entityMailBox, id);
                        socket.Send(pkg.ToBytes());
                    });
                    break;
            }
        }

        private void HandleExchangeMailBox(object arg)
        {
            var (msg, conn, id) = (arg as Tuple<IMessage, Connection, UInt32>)!;

            var gateMailBox = (msg as ExchangeMailBox)!.Mailbox;
            var socket = conn.Socket;

            Logger.Info(
                $"exchange mailbox: {gateMailBox.ID} {gateMailBox.IP} {gateMailBox.Port} {gateMailBox.HostNum}");

            conn.MailBox = RpcHelper.PbMailBoxToRpcMailBox(gateMailBox);

            var res = new ExchangeMailBoxRes()
            {
                Mailbox = RpcHelper.RpcMailBoxToPbMailBox(entity_.MailBox!),
            };

            var pkg = PackageHelper.FromProtoBuf(res, id);
            socket.Send(pkg.ToBytes());
        }
    }
}