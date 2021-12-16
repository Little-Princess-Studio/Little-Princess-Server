using System.IO;
using System;
using LPS.Core.Rpc;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace LPS.Core
{
    public class StartupManager
    {
        public static void FromConfig(string path)
        {
            var content = File.ReadAllText(path);

            var json = JObject.Parse(content, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore
            });

            var type = json["type"]?.ToString();

            if (type is not null)
            {
                switch (type)
                {
                    case "hostmanager":
                        HandleHostManagerConf(json);
                        break;
                    case "dbmanager":
                        HandleDBManagerConf(json);
                        break;
                    case "gate":
                        HandleGateConf(json);
                        break;
                    case "server":
                        HandleServerConf(json);
                        break;
                    default:
                        throw new Exception($"Wrong Config File {path}.");
                }
            }
            else
            {
                throw new Exception($"Wrong Config File {path}.");
            }
        }

        private static void HandleDBManagerConf(JObject json)
        {
            Console.WriteLine("startup dbmanager");
        }

        private static void HandleGateConf(JObject json)
        {
            Console.WriteLine("startup gates");
        }

        private static void HandleHostManagerConf(JObject json)
        {
            Console.WriteLine("startup hostmanager");
        }

        private static void HandleServerConf(JObject json)
        {
            Console.WriteLine("startup servers");
        }

    }
}
