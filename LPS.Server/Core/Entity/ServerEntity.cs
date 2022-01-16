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
        public ServerEntity(MailBox mailbox)
        {
            this.MailBox = mailbox;
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
    }
}
