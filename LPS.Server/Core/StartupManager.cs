using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using LPS.Core.Database;
using LPS.Core.Debug;
using LPS.Core.Rpc;
using Newtonsoft.Json.Linq;

namespace LPS.Core
{
    public static class StartupManager
    {
        private static JObject GetJson(string path)
        {
            var content = File.ReadAllText(path);

            var json = JObject.Parse(content, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore
            });

            return json;
        }

        public static void FromConfig(string path)
        {
            var json = GetJson(path);
            var type = json["type"]?.ToString();

            if (type is not null)
            {
                switch (type)
                {
                    case "hostmanager":
                        HandleHostManagerConf(type, path, json);
                        break;
                    case "dbmanager":
                        HandleDBManagerConf(type, path, json);
                        break;
                    case "gate":
                        HandleGateConf(type, path, json);
                        break;
                    case "server":
                        HandleServerConf(type, path, json);
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

        private static void HandleDBManagerConf(string type, string confFilePath, JObject json)
        {
            Logger.Info("startup dbmanager");

            var name = "dbmanager";
            var relativePath = GetBinPath();
            StartSubProcess(type, name, confFilePath, relativePath);
        }

        private static void HandleGateConf(string type, string confFilePath, JObject json)
        {
            Logger.Info("startup gates");

            var dict = json["gates"]!.ToObject<Dictionary<string, JToken>>();

            var relativePath = GetBinPath();
            foreach (var name in dict!.Keys)
            {
                StartSubProcess(type, name, confFilePath, relativePath);
            }
        }

        private static void HandleHostManagerConf(string type, string confFilePath, JObject json)
        {
            Logger.Info("startup hostmanager");

            var name = "hostmanager";
            var relativePath = GetBinPath();
            StartSubProcess(type, name, confFilePath, relativePath);
        }

        private static void HandleServerConf(string type, string confFilePath, JObject json)
        {
            Logger.Info("startup servers");

            var dict = json["servers"]!.ToObject<Dictionary<string, JToken>>();

            var relativePath = GetBinPath();
            foreach (var name in dict!.Keys)
            {
                StartSubProcess(type, name, confFilePath, relativePath);
            }
        }

        private static void StartSubProcess(string type, string name, string confFilePath, string binaryPath)
        {
            Logger.Info($"startup {name}");

            ProcessStartInfo procStartInfo;
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                procStartInfo = new ProcessStartInfo()
                {
                    FileName = binaryPath,
                    Arguments = $"subproc --type {type} --confpath {confFilePath} --childname {name}",
                    UseShellExecute = true,
                };
            }
            else
            {
                procStartInfo = new ProcessStartInfo()
                {
                    FileName = "dotnet",
                    Arguments = $"{binaryPath} subproc --type {type} --confpath {confFilePath} --childname {name}",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                };
            }
            
            Process.Start(procStartInfo);
        }

        private static string GetBinPath()
        {
            string relativePath;

            // Linux need to remove .dll suffix to start process
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var dirName = Path.GetDirectoryName(Path.GetRelativePath(Directory.GetCurrentDirectory(), System.Reflection.Assembly.GetExecutingAssembly().Location));
                var exeName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                relativePath = Path.Join(dirName, exeName);
            }
            else
            {
                relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), System.Reflection.Assembly.GetExecutingAssembly().Location);
            }

            return relativePath;
        }

        public static void StartUp(string type, string name, string confFilePath)
        {
            switch (type)
            {
                case "hostmanager":
                    break;
                case "dbmanager":
                    StartUpManager(name, confFilePath);
                    break;
                case "gate":
                    StartUpGate(name, confFilePath);
                    break;
                case "server":
                    StartUpServer(name, confFilePath);
                    break;
                default:
                    throw new Exception($"Wrong Config File {type} {name} {confFilePath}.");
            }
        }

        private static void StartUpManager(string name, string confFilePath)
        {
            RpcProtobufDefs.Initialize();
            DbHelper.Initialize().Wait();

            var json = GetJson(confFilePath);

            var hostnum = Convert.ToInt32(json["hostnum"]!.ToString());

            var ip = json["ip"]!.ToString();
            var port = json["port"]!.ToObject<int>();

            var hostManagerInfo = json["hostmanager"]!;
            var hostManagerIP= hostManagerInfo["ip"]!.ToString();
            var hostManagerPort = Convert.ToInt32(hostManagerInfo["port"]!.ToString());

            var globalcacheType = json["globalcache"]!["dbtype"]!.ToString();
            var globalcacheConfig = json["globalcache"]!["dbconfig"]!;
            var globalcacheIp = globalcacheConfig["ip"]!.ToString();
            var globalcachePort = globalcacheConfig["port"]!.ToObject<int>();
            var globalcacheDefaultDb = globalcacheConfig["defaultdb"]!.ToString();

            var globalCacheInfo = (globalcacheIp, globalcachePort, globalcacheDefaultDb);

            Logger.Debug($"Startup DbManager {name} at {ip}:{port}");
            var dbManager = new DbManager(ip, port, hostnum, hostManagerIP, hostManagerPort, globalCacheInfo);
            dbManager.Loop();
        }

        private static void StartUpGate(string name, string confFilePath)
        {
            RpcProtobufDefs.Initialize();
            RpcHelper.ScanRpcMethods("LPS.Core.Entity");
            DbHelper.Initialize().Wait();

            var json = GetJson(confFilePath);

            var hostnum = Convert.ToInt32(json["hostnum"]!.ToString());

            var gateInfo = json["gates"]![name]!;
            var ip = gateInfo["ip"]!.ToString();
            var port = Convert.ToInt32(gateInfo["port"]!.ToString());

            var hostManagerInfo = json["hostmanager"]!;
            var hostManagerIP= hostManagerInfo["ip"]!.ToString();
            var hostManagerPort = Convert.ToInt32(hostManagerInfo["port"]!.ToString());

            #region get servers' ip/port
            var serverJson = GetJson(json["server_conf"]!.ToString());
            var dict = serverJson["servers"]!.ToObject<Dictionary<string, JToken>>();

            var servers = dict!.Select(pair => (
                pair.Value["ip"]!.ToString(), pair.Value["port"]!.ToObject<int>())).ToArray();
            #endregion

            #region get other gate's ip/port
            var otherGates = json["gates"]!.ToObject<Dictionary<string, JToken>>()!
                                        .Where(pair => pair.Key != name)
                                        .Select(
                                            pair => (pair.Value["innerip"]!.ToString(), pair.Value["ip"]!.ToString(), pair
                                            .Value["port"]!
                                            .ToObject<int>()))
                                        .ToArray();
            #endregion

            Logger.Debug($"Startup Gate {name} at {ip}:{port}");
            var gate = new Gate(name, ip, port, hostnum, hostManagerIP, hostManagerPort, servers, otherGates);
            gate.Loop();
        } 

        private static void StartUpServer(string name, string confFilePath)
        {
            RpcProtobufDefs.Initialize();
            RpcHelper.ScanRpcMethods("LPS.Core.Entity");
            RpcHelper.ScanRpcMethods("LPS.Logic.Entity");
            RpcHelper.ScanRpcPropertyContainer("LPS.Logic.RpcProperties");
            DbHelper.Initialize().Wait();

            var json = GetJson(confFilePath);

            var hostnum = Convert.ToInt32(json["hostnum"]!.ToString());

            var serverInfo = json["servers"]![name]!;
            var ip = serverInfo["ip"]!.ToString();
            var port = Convert.ToInt32(serverInfo["port"]!.ToString());

            var hostManagerInfo = json["hostmanager"]!;
            var hostManagerIP= hostManagerInfo["ip"]!.ToString();
            var hostManagerPort = Convert.ToInt32(hostManagerInfo["port"]!.ToString());

            Logger.Debug($"Startup Server {name} at {ip}:{port}");
            var server = new Server(name, ip, port, hostnum, hostManagerIP, hostManagerPort);

            server.Loop();
        }

    }
}
