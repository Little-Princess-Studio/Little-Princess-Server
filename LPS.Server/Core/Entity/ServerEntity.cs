using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPS.Core.Database;
using LPS.Core.Debug;
using LPS.Core.Rpc;
using MailBox = LPS.Core.Rpc.MailBox;

namespace LPS.Core.Entity
{
    [EntityClass]
    public class ServerEntity : UniqueEntity
    {
        private readonly Action<string, string, MailBox> onCreateEntity_;

        public ServerEntity(MailBox mailbox, Action<string, string, MailBox> onCreateEntity)
        {
            this.MailBox = mailbox;
            onCreateEntity_ = onCreateEntity;
        }

        [RpcMethod(Authority.All)]
        public void Echo(string msg)
        {
            Logger.Info($"Echo Echo Echo {msg}");
        }

        [RpcMethod(Authority.ServerOnly)]
        public void Echo2(Dictionary<string, string> testMap, int testInt, float testFloat, List<string> testList)
        {
            Logger.Info("Echo Echo Echo");
            Logger.Info($"{testInt}");
            Logger.Info($"{testFloat}");
            Logger.Info($"{string.Join(',', testMap.Select(pair => '<' + pair.Key + ',' + pair.Value + '>'))}");
            Logger.Info($"{string.Join(',', testList)}");
        }

        [RpcMethod(Authority.ServerOnly)]
        public ValueTask Echo3(MailBox mb)
        {
            Logger.Info($"Got mailbox {mb}");
            // await this.Call(mb, "Hello, LPS");
            return new ValueTask();
        }

        [RpcMethod(Authority.ServerOnly)]
        public async Task<MailBox> CreateEntity(string entityClassName, string jsonDesc)
        {
            var newId = await DbHelper.GenerateNewGlobalId();

            var newMailBox = new MailBox(
                newId, this.MailBox.Ip,
                this.MailBox.Port,
                this.MailBox.HostNum);

            try
            {
                onCreateEntity_.Invoke(entityClassName, jsonDesc, newMailBox);
            }
            catch(Exception e)
            {
                Logger.Error(e, "Create entity failed.");
                throw;
            }

            return newMailBox;
        }
    }
}
