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
    private static readonly object AliveProcessesLock = new();
    private static readonly Dictionary<string, Process> SubProcessHandles = new();

    /// <summary>
    /// Persistent record of how each known subprocess was originally spawned,
    /// so callers (the embedded supervisor HTTP endpoint, WebManager) can
    /// restart or re-spawn by name without re-parsing the config file.
    /// Keyed by instance name (same key used in <see cref="AliveProcesses"/>).
    /// </summary>
    private static readonly Dictionary<string, SubProcessSpawnSpec> SpawnSpecs = new();

    private readonly struct SubProcessSpawnSpec
    {
        public readonly string Type;
        public readonly string ConfFilePath;
        public readonly string BinaryPath;
        public readonly bool Hotreload;

        public SubProcessSpawnSpec(string type, string confFilePath, string binaryPath, bool hotreload)
        {
            this.Type = type;
            this.ConfFilePath = confFilePath;
            this.BinaryPath = binaryPath;
            this.Hotreload = hotreload;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to redirect sub-process outputs to the main process console.
    /// </summary>
    public static bool RedirectSubprocessOutput { get; set; } = false;

    /// <summary>
    /// Gets a value indicating whether the launcher is in the process of shutting down. When true,
    /// the auto-restart-on-nonzero-exit logic in <see cref="Process.Exited"/> is suppressed.
    /// </summary>
    public static bool IsShuttingDown { get; private set; }

    /// <summary>
    /// Signals graceful shutdown: stop the auto-restart loop and kill all alive subprocesses.
    /// Safe to call multiple times.
    /// </summary>
    public static void ShutdownAll()
    {
        if (IsShuttingDown)
        {
            return;
        }

        IsShuttingDown = true;
        Logger.Info("[StartupManager] Shutdown signal received, killing all subprocesses.");

        Process[] handles;
        lock (AliveProcessesLock)
        {
            handles = SubProcessHandles.Values.ToArray();
        }

        foreach (var p in handles)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[StartupManager] Failed to kill subprocess: {ex.Message}");
            }
        }

        lock (AliveProcessesLock)
        {
            AliveProcesses.Clear();
        }
    }

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
        while (true)
        {
            // The supervisor (LPS.Server.Demo.Supervisor) may stop every
            // subprocess and later restart them. We must NOT exit the
            // launcher just because the alive set is empty - that would
            // tear the supervisor HTTP down too. Only exit on an explicit
            // ShutdownAll signal.
            if (IsShuttingDown)
            {
                break;
            }

            Thread.Sleep(1000);
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

        // Remember the spawn spec so the supervisor surface (StartInstance /
        // RestartInstance) can re-spawn the same process later without having
        // to re-read its config from disk.
        lock (AliveProcessesLock)
        {
            SpawnSpecs[name] = new SubProcessSpawnSpec(type, confFilePath, binaryPath, hotreload);
        }

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
        if (RedirectSubprocessOutput)
        {
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError = true;
        }

        var process = new Process
        {
            StartInfo = procStartInfo,
            EnableRaisingEvents = true,
        };
        process.Exited += (sender, e) =>
        {
            lock (AliveProcessesLock)
            {
                SubProcessHandles.Remove(name);
            }

            if (IsShuttingDown)
            {
                Logger.Info($"subprocess {name} exited during shutdown, skip restart.");
                lock (AliveProcessesLock)
                {
                    AliveProcesses.Remove(name);
                }

                return;
            }

            // Supervisor-initiated stop: suppress auto-restart regardless of
            // exit code, then clear the guard so subsequent natural crashes
            // do trigger restart.
            bool deliberate;
            lock (AliveProcessesLock)
            {
                deliberate = DeliberatelyStopping.Remove(name);
                if (deliberate)
                {
                    AliveProcesses.Remove(name);
                }
            }

            if (deliberate)
            {
                Logger.Info($"subprocess {name} exited (supervisor-stopped), skip restart.");
                return;
            }

            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                Logger.Warn($"subprocess {name} exited with unexpected code {exitCode}, restart it.");
                StartSubProcess(type, name, confFilePath, binaryPath, hotreload, true);
            }
            else
            {
                Logger.Info($"subprocess {name} exited with expected code, exitcode: {exitCode}");
                lock (AliveProcessesLock)
                {
                    AliveProcesses.Remove(name);
                }
            }
        };

        if (RedirectSubprocessOutput)
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[{name}] {e.Data}");
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Console.Error.WriteLine($"[{name} ERROR] {e.Data}");
                }
            };
        }

        lock (AliveProcessesLock)
        {
            AliveProcesses.Add(name);
            SubProcessHandles[name] = process;
        }

        process.Start();

        if (RedirectSubprocessOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
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

        var service = new LPS.Server.Instance.Service(
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

    // ------------------------------------------------------------------
    // Supervisor API - public surface used by the embedded HTTP supervisor
    // (LPS.Server.Demo) to drive cluster-level and per-instance lifecycle
    // operations on behalf of the WebManager.
    // ------------------------------------------------------------------

    /// <summary>
    /// A snapshot row of one tracked subprocess, returned by
    /// <see cref="GetSubProcessStatus"/>. Used by the supervisor HTTP layer
    /// to render Cluster Overview rows with start/stop affordances.
    /// </summary>
    public readonly record struct SubProcessStatus(string Name, string Type, bool Alive, int Pid, bool HasExited);

    /// <summary>
    /// Snapshot of every subprocess this launcher has ever spawned, marked
    /// alive/dead based on whether <see cref="AliveProcesses"/> still
    /// contains them. Dead rows are kept so the UI can offer a "Start" button
    /// for them after a graceful shutdown.
    /// </summary>
    public static IReadOnlyList<SubProcessStatus> GetSubProcessStatus()
    {
        lock (AliveProcessesLock)
        {
            var rows = new List<SubProcessStatus>(SpawnSpecs.Count);
            foreach (var (name, spec) in SpawnSpecs)
            {
                SubProcessHandles.TryGetValue(name, out var proc);
                var alive = AliveProcesses.Contains(name);
                var pid = proc is { HasExited: false } ? proc.Id : -1;
                var hasExited = proc?.HasExited ?? true;
                rows.Add(new SubProcessStatus(name, spec.Type, alive, pid, hasExited));
            }

            return rows;
        }
    }

    /// <summary>
    /// (Re)spawn one named subprocess. If a process with the same name is
    /// currently alive this is a no-op (use <see cref="RestartInstance"/> for
    /// that). The spawn spec must have been recorded by a previous boot
    /// (i.e. <see cref="FromConfig"/> was called for this name at startup) -
    /// the supervisor never invents new instances out of thin air.
    /// </summary>
    /// <returns>true if a new process was spawned, false otherwise.</returns>
    public static bool StartInstance(string name)
    {
        SubProcessSpawnSpec spec;
        lock (AliveProcessesLock)
        {
            if (AliveProcesses.Contains(name))
            {
                Logger.Info($"[supervisor] StartInstance({name}) ignored - already alive.");
                return false;
            }

            if (!SpawnSpecs.TryGetValue(name, out spec))
            {
                Logger.Warn($"[supervisor] StartInstance({name}) rejected - no spawn spec on record.");
                return false;
            }
        }

        StartSubProcess(spec.Type, name, spec.ConfFilePath, spec.BinaryPath, spec.Hotreload, false);
        return true;
    }

    /// <summary>
    /// Force-kill one subprocess (entire process tree). Bypasses the graceful
    /// drain path; use only when the in-band ShutdownInstance HostCommand
    /// fails or the target is unresponsive. <see cref="IsShuttingDown"/> is
    /// NOT toggled, so other instances remain auto-restartable.
    /// </summary>
    public static bool StopInstance(string name)
    {
        Process? proc;
        lock (AliveProcessesLock)
        {
            if (!SubProcessHandles.TryGetValue(name, out proc))
            {
                return false;
            }
        }

        try
        {
            if (!proc.HasExited)
            {
                // Mark name as "shutting down" by removing from AliveProcesses
                // BEFORE killing, so the Exited handler's restart branch sees
                // a non-zero exit but skips restart for THIS one name via the
                // dedicated guard below.
                lock (AliveProcessesLock)
                {
                    DeliberatelyStopping.Add(name);
                }

                proc.Kill(entireProcessTree: true);
                Logger.Info($"[supervisor] StopInstance({name}) killed pid={proc.Id}.");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[supervisor] StopInstance({name}) failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Kill the subprocess (if alive) and spawn a fresh one with the recorded
    /// spec. The original auto-restart loop is NOT used here because it
    /// fires only on non-zero exit; supervisor restarts are deliberate and
    /// must always succeed.
    /// </summary>
    public static bool RestartInstance(string name)
    {
        StopInstance(name);

        // Wait briefly for the process to die so port bindings clear before
        // the replacement comes up. The Kill itself is synchronous on Win,
        // but the OS may keep the TCP socket in TIME_WAIT - the per-instance
        // bind code is robust to this so a short wait is sufficient.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            lock (AliveProcessesLock)
            {
                if (!AliveProcesses.Contains(name) && !SubProcessHandles.ContainsKey(name))
                {
                    break;
                }
            }

            Thread.Sleep(100);
        }

        return StartInstance(name);
    }

    /// <summary>
    /// Force-stop every subprocess but do NOT mark the launcher itself as
    /// shutting down. Used by the supervisor's "cluster stop" endpoint so a
    /// subsequent "cluster start" can re-spawn everything in the same
    /// launcher process.
    /// </summary>
    public static void StopAllInstances()
    {
        Process[] procs;
        string[] names;
        lock (AliveProcessesLock)
        {
            procs = SubProcessHandles.Values.ToArray();
            names = SubProcessHandles.Keys.ToArray();
            foreach (var n in names)
            {
                DeliberatelyStopping.Add(n);
            }
        }

        foreach (var p in procs)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[supervisor] StopAllInstances kill failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Re-spawn every recorded instance that is currently dead. Used by the
    /// supervisor's "cluster start" endpoint after a prior stop. Returns the
    /// count of processes actually started.
    /// </summary>
    public static int StartAllInstances()
    {
        string[] names;
        lock (AliveProcessesLock)
        {
            names = SpawnSpecs.Keys.ToArray();
        }

        var started = 0;
        foreach (var n in names)
        {
            if (StartInstance(n))
            {
                started++;
            }
        }

        return started;
    }

    /// <summary>
    /// Set of names the supervisor is intentionally stopping. The
    /// Process.Exited handler consults this and skips its auto-restart
    /// branch even on non-zero exits when the name is present.
    /// </summary>
    private static readonly HashSet<string> DeliberatelyStopping = new();
}