// -----------------------------------------------------------------------
// <copyright file="StartupManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Database;
using LPS.Server.Instance;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using Newtonsoft.Json.Linq;

/// <summary>
/// Class to control the startup of all the processes of the host.
/// </summary>
public static class StartupManager
{
    /// <summary>
    /// Startup a process via config file.
    /// </summary>
    /// <param name="path">Config file path.</param>
    /// <param name="hotreaload">If enable hotreload for process.</param>
    /// <exception cref="Exception">Throw exception if failed to startup a process.</exception>
    public static void FromConfig(string path, bool hotreaload)
    {
        var json = GetJson(path);
        var type = json["type"]?.ToString();

        if (type is not null)
        {
            switch (type)
            {
                case "hostmanager":
                    HandleHostManagerConf(type, path, json, hotreaload);
                    break;
                case "dbmanager":
                    HandleDbManagerConf(type, path, json, hotreaload);
                    break;
                case "gate":
                    HandleGateConf(type, path, json, hotreaload);
                    break;
                case "server":
                    HandleServerConf(type, path, json, hotreaload);
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

    /// <summary>
    /// Startup a process.
    /// </summary>
    /// <param name="type">Process type, one of hostmanager/dbmanager/gate/server.</param>
    /// <param name="name">Name of the process.</param>
    /// <param name="confFilePath">Config file path.</param>
    /// <exception cref="Exception">Throw exception if failed to startup the process.</exception>
    public static void StartUp(string type, string name, string confFilePath)
    {
        switch (type)
        {
            case "hostmanager":
                StartUpHostManager(name, confFilePath);
                break;
            case "dbmanager":
                StartUpDbManager(name, confFilePath);
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

    private static JObject GetJson(string path)
    {
        var content = File.ReadAllText(path);

        var json = JObject.Parse(content, new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
        });

        return json;
    }

    private static void HandleDbManagerConf(string type, string confFilePath, JObject json, bool hotreload)
    {
        Logger.Info("startup dbmanager");

        var name = "dbmanager";
        var relativePath = GetBinPath();
        StartSubProcess(type, name, confFilePath, relativePath, hotreload);
    }

    private static void HandleGateConf(string type, string confFilePath, JObject json, bool hotreload)
    {
        Logger.Info("startup gates");

        var dict = json["gates"]!.ToObject<Dictionary<string, JToken>>();

        var relativePath = GetBinPath();
        foreach (var name in dict!.Keys)
        {
            StartSubProcess(type, name, confFilePath, relativePath, hotreload);
        }
    }

    private static void HandleHostManagerConf(string type, string confFilePath, JObject json, bool hotreload)
    {
        Logger.Info("startup hostmanager");

        var name = "hostmanager";
        var relativePath = GetBinPath();
        StartSubProcess(type, name, confFilePath, relativePath, hotreload);
    }

    private static void HandleServerConf(string type, string confFilePath, JObject json, bool hotreload)
    {
        Logger.Info("startup servers");

        var dict = json["servers"]!.ToObject<Dictionary<string, JToken>>();

        var relativePath = GetBinPath();
        foreach (var name in dict!.Keys)
        {
            StartSubProcess(type, name, confFilePath, relativePath, hotreload);
        }
    }

    private static void StartSubProcess(
        string type, string name, string confFilePath, string binaryPath, bool hotreload)
    {
        Logger.Info($"startup {name}");

        ProcessStartInfo procStartInfo;
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            if (!hotreload)
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
                    Arguments = $"watch run subproc --type {type} --confpath {confFilePath} --childname {name}",
                    UseShellExecute = true,
                };
            }
        }
        else
        {
            if (!hotreload)
            {
                procStartInfo = new ProcessStartInfo()
                {
                    FileName = "dotnet",
                    Arguments = $"{binaryPath} subproc --type {type} --confpath {confFilePath} --childname {name}",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };
            }
            else
            {
                procStartInfo = new ProcessStartInfo()
                {
                    FileName = "dotnet",
                    Arguments = $"watch run subproc --type {type} --confpath {confFilePath} --childname {name}",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };
            }
        }

        Process.Start(procStartInfo);
    }

    private static string GetBinPath()
    {
        string relativePath;

        // Linux need to remove .dll suffix to start process
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var dirName = Path.GetDirectoryName(Path.GetRelativePath(
                Directory.GetCurrentDirectory(),
                System.Reflection.Assembly.GetExecutingAssembly().Location));
            var exeName = System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name;
            relativePath = Path.Join(dirName, exeName);
        }
        else
        {
            relativePath = Path.GetRelativePath(
                Directory.GetCurrentDirectory(),
                System.Reflection.Assembly.GetEntryAssembly()!.Location);
        }

        return relativePath;
    }

    private static void StartUpHostManager(string name, string confFilePath)
    {
        RpcProtobufDefs.Initialize();

        var json = GetJson(confFilePath);

        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;
        DbHelper.Initialize(globalCacheConf, name).Wait();

        var hostnum = Convert.ToInt32(json["hostnum"]!.ToString());
        var ip = json["ip"]!.ToString();
        var port = json["port"]!.ToObject<int>();

        var serverNum = json["server_num"]!.ToObject<int>();
        var gateNum = json["gate_num"]!.ToObject<int>();

        var hostManager = new HostManager(name, hostnum, ip, port, serverNum, gateNum);

        ServerGlobal.Init(hostManager);

        hostManager.Loop();
    }

    private static void StartUpDbManager(string name, string confFilePath)
    {
        RpcProtobufDefs.Initialize();

        var json = GetJson(confFilePath);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;

        DbHelper.DbInfo? databaseConf = json["database"]!.ToObject<DbHelper.DbInfo>()!;

        // DbHelper.Initialize(globalCacheConf, name).Wait();
        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var ip = json["ip"]!.ToString();
        var port = json["port"]!.ToObject<int>();

        var hostMgrConf = GetJson(json["hostmanager_conf"]!.ToString())!;
        var hostnum = Convert.ToInt32(hostMgrConf["hostnum"]!.ToString());
        var hostManagerIp = hostMgrConf["ip"]!.ToString();
        var hostManagerPort = Convert.ToInt32(hostMgrConf["port"]!.ToString());

        var databaseApiProviderNamespace = json["db_api_provider_namespace"]!.ToString();

        Logger.Debug($"Startup DbManager {name} at {ip}:{port}");
        var databaseManager = new DbManager(
            ip,
            port,
            hostnum,
            hostManagerIp,
            hostManagerPort,
            globalCacheConf,
            databaseConf,
            databaseApiProviderNamespace);

        ServerGlobal.Init(databaseManager);

        databaseManager.Loop();
    }

    private static void StartUpGate(string name, string confFilePath)
    {
        RpcProtobufDefs.Initialize();
        var extraAssemblies = new System.Reflection.Assembly[] { typeof(StartupManager).Assembly };
        RpcHelper.ScanRpcMethods(new[] { "LPS.Server.Entity" }, extraAssemblies);

        var json = GetJson(confFilePath);

        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;
        DbHelper.Initialize(globalCacheConf, name).Wait();

        var gateInfo = json["gates"]![name]!;
        var ip = gateInfo["ip"]!.ToString();
        var port = Convert.ToInt32(gateInfo["port"]!.ToString());
        var useMqToHost = Convert.ToBoolean(gateInfo["use_mq_to_host"]!.ToString());

        var hostMgrConf = GetJson(json["hostmanager_conf"]!.ToString())!;
        var hostnum = Convert.ToInt32(hostMgrConf["hostnum"]!.ToString());
        var hostManagerIp = hostMgrConf["ip"]!.ToString();
        var hostManagerPort = Convert.ToInt32(hostMgrConf["port"]!.ToString());

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

        Logger.Debug($"Startup Gate {name} at {ip}:{port}, use mq: {useMqToHost}");
        var gate = new Gate(name, ip, port, hostnum, hostManagerIp, hostManagerPort, servers, otherGates, useMqToHost);

        ServerGlobal.Init(gate);

        gate.Loop();
    }

    private static void StartUpServer(string name, string confFilePath)
    {
        RpcProtobufDefs.Initialize();

        var json = GetJson(confFilePath);
        var entityNamespace = json["entity_namespace"]!.ToString();
        var rpcPropertyNamespace = json["rpc_property_namespace"]!.ToString();
        var rpcStubInterfaceNamespace = json["rpc_stub_interface_namespace"]!.ToString();

        var extraAssemblies = new System.Reflection.Assembly[] { typeof(StartupManager).Assembly };
        RpcHelper.ScanRpcMethods(new[] { "LPS.Server.Entity", entityNamespace }, extraAssemblies);
        RpcHelper.ScanRpcPropertyContainer(rpcPropertyNamespace, extraAssemblies);
        RpcStubGeneratorManager.ScanAndBuildGenerator(
            new[] { "LPS.Common.Entity", "LPS.Server.Entity", entityNamespace },
            new[] { rpcStubInterfaceNamespace },
            extraAssemblies);

        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;
        DbHelper.Initialize(globalCacheConf, name).Wait();

        var serverInfo = json["servers"]![name]!;
        var ip = serverInfo["ip"]!.ToString();
        var port = Convert.ToInt32(serverInfo["port"]!.ToString());
        var useMqToHost = Convert.ToBoolean(serverInfo["use_mq_to_host"]!.ToString());

        var hostMgrConf = GetJson(json["hostmanager_conf"]!.ToString())!;
        var hostnum = Convert.ToInt32(hostMgrConf["hostnum"]!.ToString());
        var hostManagerIp = hostMgrConf["ip"]!.ToString();
        var hostManagerPort = Convert.ToInt32(hostMgrConf["port"]!.ToString());

        Logger.Debug($"Startup Server {name} at {ip}:{port}, use mq: {useMqToHost}");
        var server = new Server(name, ip, port, hostnum, hostManagerIp, hostManagerPort, useMqToHost);

        ServerGlobal.Init(server);

        server.Loop();
    }
}