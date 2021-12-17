using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LPS.Core.Debug;
using Newtonsoft.Json.Linq;

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
                        HandleHostManagerConf(path, json);
                        break;
                    case "dbmanager":
                        HandleDBManagerConf(path, json);
                        break;
                    case "gate":
                        HandleGateConf(path, json);
                        break;
                    case "server":
                        HandleServerConf(path, json);
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

        private static void HandleDBManagerConf(string path, JObject json)
        {
            Logger.Info("startup dbmanager");
        }

        private static void HandleGateConf(string path, JObject json)
        {
            Logger.Info("startup gates");

            var dict = json["gates"].ToObject<Dictionary<string, JToken>>();

            string relativePath = String.Empty;

            // Linux need to remove .dll suffix to start process
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var dirName = Path.GetDirectoryName(Path.GetRelativePath(Directory.GetCurrentDirectory(), System.Reflection.Assembly.GetExecutingAssembly().Location));
                var exeName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                relativePath = Path.Join(dirName, exeName);
            }
            else
            {
                // todo: test execute the process on windows
            }

            foreach (var name in dict.Keys)
            {
                Logger.Info($"startup {name}");

                var procStartInfo = new ProcessStartInfo()
                {
                    FileName = relativePath,
                    Arguments = $"subproc --confpath {path} --childname {name}",
                    UseShellExecute = false,
                };
                Process.Start(procStartInfo);
            }
        }

        private static void HandleHostManagerConf(string path, JObject json)
        {
            Logger.Info("startup hostmanager");
        }

        private static void HandleServerConf(string path, JObject json)
        {
            Logger.Info("startup servers");
        }

    }
}
