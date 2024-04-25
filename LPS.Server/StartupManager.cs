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
using System.Reflection;
using System.Threading;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Database;
using LPS.Server.Instance;
using LPS.Server.MessageQueue;
using LPS.Server.Rpc;
using LPS.Server.Service;
using LPS.Service.Instance;
using Newtonsoft.Json.Linq;

/// <summary>
/// Class to control the startup of all the processes of the host.
/// </summary>
public static class StartupManager
{
    /// <summary>
    /// Class to store the information of a subprocess.
    /// </summary>
    public readonly struct SubProcessStartupInfo
    {
        /// <summary>
        /// The type of the subprocess.
        /// </summary>
        public readonly string Type;

        /// <summary>
        /// The name of the instance of the subprocess.
        /// </summary>
        public readonly string InstanceName;

        /// <summary>
        /// The path to the configuration file for the subprocess.
        /// </summary>
        public readonly string ConfFilePath;

        /// <summary>
        /// The path to the binary file of the subprocess.
        /// </summary>
        public readonly string BinaryPath;

        /// <summary>
        /// Is this subprocess restarting.
        /// </summary>
        public readonly bool IsRestart;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubProcessStartupInfo"/> struct.
        /// </summary>
        /// <param name="type">The type of the subprocess.</param>
        /// <param name="instanceName">The name of the instance.</param>
        /// <param name="confFilePath">The path to the configuration file.</param>
        /// <param name="binaryPath">The path to the binary file.</param>
        /// <param name="isRestart">Is this subprocess restarting.</param>
        public SubProcessStartupInfo(string type, string instanceName, string confFilePath, string binaryPath, bool isRestart)
        {
            this.Type = type;
            this.InstanceName = instanceName;
            this.ConfFilePath = confFilePath;
            this.BinaryPath = binaryPath;
            this.IsRestart = isRestart;
        }
    }

    /// <summary>
    /// Gets or sets the function to get the startup arguments string for a subprocess.
    /// </summary>
    public static Func<SubProcessStartupInfo, string> OnGetStartupArgumentsString = null!;

    private static readonly HashSet<string> AliveProcesses = new HashSet<string>();

    /// <summary>
    /// Startup a process via config file.
    /// </summary>
    /// <param name="path">Config file path.</param>
    /// <param name="hotreaload">If enable hotreload for process.</param>
    /// <param name="isRestart">Is this sub process restarting.</param>
    /// <exception cref="Exception">Throw exception if failed to startup a process.</exception>
    public static void FromConfig(string path, bool hotreaload, bool isRestart)
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
                    HandleGateConf(type, path, json, hotreaload, isRestart);
                    break;
                case "server":
                    HandleServerConf(type, path, json, hotreaload, isRestart);
                    break;
                case "service":
                    HandleServiceConf(type, path, json, hotreaload);
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
    /// <param name="restart">Is restart this instance.</param>
    /// <exception cref="Exception">Throw exception if failed to startup the process.</exception>
    public static void StartUp(string type, string name, string confFilePath, bool restart)
    {
        if (OnGetStartupArgumentsString is null)
        {
            throw new Exception("Method of GetStartupArgumentsString is not set.");
        }

        switch (type)
        {
            case "hostmanager":
                StartUpHostManager(name, confFilePath);
                break;
            case "dbmanager":
                StartUpDbManager(name, confFilePath);
                break;
            case "gate":
                StartUpGate(name, confFilePath, restart);
                break;
            case "server":
                StartUpServer(name, confFilePath, restart);
                break;
            case "servicemanager":
                StartUpServiceManager(name, confFilePath);
                break;
            case "service":
                StartUpService(name, confFilePath);
                break;
            default:
                throw new Exception($"Wrong Config File {type} {name} {confFilePath}.");
        }
    }

    /// <summary>
    /// Watches all sub-processes.
    /// </summary>
    public static void WatchAllSubProcesses()
    {
        Logger.Info("Start watching all sub processes");
        while (AliveProcesses.Count > 0)
        {
            Thread.Sleep(10000);
        }

        Logger.Info("All sub processes exited, exit watching process");
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
        StartSubProcess(type, name, confFilePath, relativePath, hotreload, false);
    }

    private static void HandleGateConf(string type, string confFilePath, JObject json, bool hotreload, bool isRestart)
    {
        Logger.Info("startup gates");

        var dict = json["gates"]!.ToObject<Dictionary<string, JToken>>();

        var relativePath = GetBinPath();
        foreach (var name in dict!.Keys)
        {
            StartSubProcess(type, name, confFilePath, relativePath, hotreload, isRestart);
        }
    }

    private static void HandleHostManagerConf(string type, string confFilePath, JObject json, bool hotreload)
    {
        Logger.Info("startup hostmanager");

        var name = "hostmanager";
        var relativePath = GetBinPath();
        StartSubProcess(type, name, confFilePath, relativePath, hotreload, false);
    }

    private static void HandleServerConf(string type, string confFilePath, JObject json, bool hotreload, bool isRestart)
    {
        Logger.Info("startup servers");

        var dict = json["servers"]!.ToObject<Dictionary<string, JToken>>();

        var relativePath = GetBinPath();
        foreach (var name in dict!.Keys)
        {
            StartSubProcess(type, name, confFilePath, relativePath, hotreload, isRestart);
        }
    }

    private static void HandleServiceConf(string type, string confFilePath, JObject json, bool hotreload)
    {
        Logger.Info("startup service manager");

        var relativePath = GetBinPath();
        var name = "servicemanager";

        StartSubProcess("servicemanager", name, confFilePath, relativePath, hotreload, false);

        var services = json["services"]!.ToObject<Dictionary<string, JToken>>();
        foreach (var serviceName in services!.Keys)
        {
            StartSubProcess("service", serviceName, confFilePath, relativePath, hotreload, false);
        }
    }

    private static void StartSubProcess(
        string type, string name, string confFilePath, string binaryPath, bool hotreload, bool isRestart)
    {
        Logger.Info($"startup {name}");

        var startUpArgumentsString =
            OnGetStartupArgumentsString(new SubProcessStartupInfo(type, name, confFilePath, binaryPath, isRestart));

        Logger.Debug($"start up arguments string: {startUpArgumentsString}");

        ProcessStartInfo procStartInfo;
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            if (!hotreload)
            {
                procStartInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = $"{startUpArgumentsString}",
                    UseShellExecute = true,
                };
            }
            else
            {
                procStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"watch run {startUpArgumentsString}",
                    UseShellExecute = true,
                };
            }
        }
        else
        {
            if (!hotreload)
            {
                procStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"{binaryPath} {startUpArgumentsString}",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };
            }
            else
            {
                procStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"watch run {startUpArgumentsString}",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };
            }
        }

        // var process = Process.Start(procStartInfo);
        var process = new Process
        {
            StartInfo = procStartInfo,
            EnableRaisingEvents = true,
        };
        process.Exited += (sender, e) =>
        {
            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                Logger.Warn("subprocess exited with unexpected code, restart it, exitcode: {exitCode}");
                StartSubProcess(type, name, confFilePath, binaryPath, hotreload, true);
            }
            else
            {
                Logger.Info($"subprocess {name} exited with expected code, exitcode: {exitCode}");
                AliveProcesses.Remove(name);
            }
        };

        AliveProcesses.Add(name);

        process.Start();
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

        var hostManager = new HostManager(
            name,
            hostnum,
            ip,
            port,
            serverNum,
            gateNum,
            json);

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

        var hostMgrConf = GetJson(path: json["hostmanager_conf"]!.ToString())!;
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
            databaseApiProviderNamespace,
            json);

        ServerGlobal.Init(databaseManager);

        databaseManager.Loop();
    }

    private static void StartUpGate(string name, string confFilePath, bool restart)
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
        var gate = new Gate(
            name,
            ip,
            port,
            hostnum,
            hostManagerIp,
            hostManagerPort,
            servers,
            otherGates,
            useMqToHost,
            json,
            restart);

        ServerGlobal.Init(gate);

        gate.Loop();
    }

    private static void StartUpServer(string name, string confFilePath, bool restart)
    {
        RpcProtobufDefs.Initialize();

        var json = GetJson(path: confFilePath);
        var entityNamespace = json["entity_namespace"]!.ToString();
        var rpcPropertyNamespace = json["rpc_property_namespace"]!.ToString();
        var rpcStubInterfaceNamespace = json["rpc_stub_interface_namespace"]!.ToString();

        var extraAssemblies = new System.Reflection.Assembly[] { typeof(StartupManager).Assembly };
        RpcHelper.ScanRpcMethods(new[] { "LPS.Server.Entity", entityNamespace }, extraAssemblies);
        RpcHelper.ScanRpcPropertyContainer(rpcPropertyNamespace, extraAssemblies);
        RpcStubGeneratorManager.ScanAndBuildGenerator(
            new[] { rpcStubInterfaceNamespace },
            new[] { typeof(RpcStubForServerClientAttribute) },
            extraAssemblies);

        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;
        DbHelper.Initialize(globalCacheConf, name).Wait();

        var serverInfo = json[propertyName: "servers"]![name]!;
        var ip = serverInfo["ip"]!.ToString();
        var port = Convert.ToInt32(serverInfo["port"]!.ToString());
        var useMqToHost = Convert.ToBoolean(serverInfo["use_mq_to_host"]!.ToString());

        var hostMgrConf = GetJson(json["hostmanager_conf"]!.ToString())!;
        var hostnum = Convert.ToInt32(hostMgrConf["hostnum"]!.ToString());
        var hostManagerIp = hostMgrConf["ip"]!.ToString();
        var hostManagerPort = Convert.ToInt32(hostMgrConf["port"]!.ToString());

        Logger.Debug($"Startup Server {name} at {ip}:{port}, use mq: {useMqToHost} restart: {restart}");
        var server = new Server(name, ip, port, hostnum, hostManagerIp, hostManagerPort, useMqToHost, json, restart);

        ServerGlobal.Init(server);

        server.Loop();
    }

    private static void StartUpServiceManager(string name, string confFilePath)
    {
        RpcProtobufDefs.Initialize();

        var json = GetJson(path: confFilePath);

        var extraAssemblies = new Assembly[] { typeof(StartupManager).Assembly };
        var serviceNamespace = json["service_namespace"]!.ToString();
        ServiceHelper.ScanServices(serviceNamespace, extraAssemblies);
        ServiceHelper.ScanRpcMethods(new[] { serviceNamespace }, extraAssemblies);

        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;
        DbHelper.Initialize(globalCacheConf, name).Wait();

        var serviceMgrInfo = json[propertyName: "service_manager"]!;
        var ip = serviceMgrInfo["ip"]!.ToString();
        var port = Convert.ToInt32(serviceMgrInfo["port"]!.ToString());
        var useMqToHost = Convert.ToBoolean(serviceMgrInfo["use_mq_to_host"]!.ToString());

        var hostMgrConf = GetJson(json["hostmanager_conf"]!.ToString())!;
        var hostnum = Convert.ToInt32(hostMgrConf["hostnum"]!.ToString());
        var hostManagerIp = hostMgrConf["ip"]!.ToString();
        var hostManagerPort = Convert.ToInt32(hostMgrConf["port"]!.ToString());

        var serviceInfo = json[propertyName: "services"]!;
        var serviceCnt = serviceInfo.Count();

        Logger.Debug($"Startup Service Manager {name} at {ip}:{port}, use mq: {useMqToHost}");
        var serviceMgr = new ServiceManager(
            name,
            ip,
            port,
            hostnum,
            hostManagerIp,
            hostManagerPort,
            useMqToHost,
            serviceCnt,
            json);

        ServerGlobal.Init(serviceMgr);

        serviceMgr.Loop();
    }

    private static void StartUpService(string name, string confFilePath)
    {
        RpcProtobufDefs.Initialize();

        var json = GetJson(path: confFilePath);

        var extraAssemblies = new System.Reflection.Assembly[] { typeof(StartupManager).Assembly };
        var serviceNamespace = json["service_namespace"]!.ToString();
        ServiceHelper.ScanServices(serviceNamespace, extraAssemblies);

        var messageQueueConf = GetJson(json["mq_conf"]!.ToString()).ToObject<MessageQueueClient.MqConfig>()!;
        MessageQueueClient.InitConnectionFactory(messageQueueConf);

        var globalCacheConf = GetJson(json["globalcache_conf"]!.ToString())!
            .ToObject<DbHelper.DbInfo>()!;
        DbHelper.Initialize(globalCacheConf, name).Wait();

        var serviceMgrInfo = json[propertyName: "service_manager"]!;
        var serviceMgrIp = serviceMgrInfo["ip"]!.ToString();
        var serviceMgrPort = Convert.ToInt32(serviceMgrInfo["port"]!.ToString());

        var serviceConf = json["services"]![name]!;
        var ip = serviceConf["ip"]!.ToString();
        var port = Convert.ToInt32(serviceConf["port"]!.ToString());

        var hostMgrConf = GetJson(path: json["hostmanager_conf"]!.ToString())!;
        var hostnum = Convert.ToInt32(hostMgrConf["hostnum"]!.ToString());

        RpcHelper.ScanRpcMethods(
            new string[] { serviceNamespace },
            typeof(BaseService),
            typeof(ServiceAttribute),
            type => type.GetCustomAttribute<ServiceAttribute>()!.ServiceName,
            extraAssemblies);

        Logger.Debug($"Start up Service {name} at {ip}:{port}");

        var service = new LPS.Service.Instance.Service(
            serviceMgrIp,
            serviceMgrPort,
            name,
            ip,
            port,
            hostnum,
            json);

        ServerGlobal.Init(service);

        service.Loop();
    }
}