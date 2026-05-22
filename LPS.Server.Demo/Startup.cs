// -----------------------------------------------------------------------
// <copyright file="Startup.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo;

using System;
using System.Collections.Generic;
using CommandLine;
using LPS.Common.Debug;

/// <summary>
/// Entry class.
/// </summary>
public static class Startup
{
#pragma warning disable CS8618
    /// <summary>
    /// Options for startup verb.
    /// </summary>
    [Verb("startup", HelpText = "Startup with a set of path")]
    private class StartUpOptions
    {
        [Option('p', "paths", Required = true, HelpText = "Set startup config pathes.")]
        public List<string> Pathes { get; set; }

        [Option('h', "hotreload", Required = true, HelpText = "Set if hot reload enabled.")]
        public bool HotReload { get; set; }
    }

    /// <summary>
    /// Options for bydefault verb.
    /// </summary>
    [Verb("bydefault", HelpText = "Startup by default")]
    private class ByDefaultOptions
    {
        [Option('h', "hotreload", Required = false, HelpText = "Set if hot reload enabled.")]
        public int HotReload { get; set; }

        [Option("headless", Required = false, HelpText = "Run subprocesses headlessly by redirecting output.")]
        public bool Headless { get; set; }
    }

    /// <summary>
    /// Options for subproc verb.
    /// </summary>
    [Verb("subproc", HelpText = "Startup sub process")]
    private class SubProcOptions
    {
        [Option("type", Required = true, HelpText = "Set up the child process type")]
        public string Type { get; set; }

        [Option("confpath", Required = true, HelpText = "Set up the child process file path")]
        public string ConfPath { get; set; }

        [Option("childname", Required = true, HelpText = "Set up the child process name in conf")]
        public string ChildName { get; set; }

        [Option("restart", Required = false, HelpText = "Set If this process is restarting")]
        public int Restart { get; set; }
    }
#pragma warning restore CS8618

    /// <summary>
    /// Startup entry.
    /// </summary>
    /// <param name="args">Args.</param>
    public static void Main(string[] args)
    {
        StartupManager.OnGetStartupArgumentsString =
            info =>
                $"subproc --type {info.Type}" +
                $" --confpath {info.ConfFilePath}" +
                $" --childname {info.InstanceName}" +
                $" --restart {(info.IsRestart ? 1 : 0)}";

        Parser.Default.ParseArguments<StartUpOptions, ByDefaultOptions, SubProcOptions>(args)
            .MapResult(
                (StartUpOptions opts) =>
                {
                    Logger.Init("startup");
                    Logger.Info("Start up with config files");
                    foreach (var path in opts.Pathes)
                    {
                        Logger.Info($"Parsing Config {path}");
                        StartupManager.FromConfig(path, opts.HotReload, false);
                    }

                    Logger.Info("Start up succ");
                    StartupManager.WatchAllSubProcesses();
                    return true;
                },
                (ByDefaultOptions opts) =>
                {
                    Logger.Init("startup");
                    Logger.Info($"Start up by default, hotreload = {opts.HotReload}");
                    if (opts.Headless)
                    {
                        StartupManager.RedirectSubprocessOutput = true;
                        StartStdinShutdownListener();
                    }

                    StartupByDefault(opts.HotReload == 1);
                    Logger.Info("Start up succ");
                    return true;
                },
                (SubProcOptions opts) =>
                {
                    Logger.Init(opts.ChildName);
                    Logger.Info($"start {opts.ChildName} {opts.Type} {opts.ConfPath} {opts.Restart}");

                    try
                    {
                        StartupManager.StartUp(opts.Type, opts.ChildName, opts.ConfPath, opts.Restart == 1);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Unhandled Error");
                    }

                    return true;
                },
                _ =>
                {
                    Logger.Warn("Wrong cmd params");
                    return false;
                });
        Thread.Sleep(10000);
    }

    private static void StartupByDefault(bool hotreload)
    {
        StartupManager.FromConfig("Config/host0/hostmanager.conf.json", hotreload, false);
        StartupManager.FromConfig("Config/host0/gate.conf.json", hotreload, false);
        StartupManager.FromConfig("Config/host0/server.conf.json", hotreload, false);
        StartupManager.FromConfig("Config/host0/dbmanager.conf.json", hotreload, false);
        StartupManager.FromConfig("Config/host0/service.conf.json", hotreload, false);

        // Start the supervisor HTTP after FromConfig has registered every
        // subprocess's spawn spec, so /supervisor/status reports the full
        // roster from the first request onward.
        Supervisor.Start();

        StartupManager.WatchAllSubProcesses();
    }

    /// <summary>
    /// In headless mode the launcher cannot rely on Ctrl-C from a TTY; the orchestrator
    /// (e.g. verify_e2e.py) instead writes the line "shutdown" to stdin. This background
    /// thread listens for that line and triggers a graceful kill of every subprocess so
    /// auto-restart does not fire.
    /// </summary>
    private static void StartStdinShutdownListener()
    {
        var t = new Thread(() =>
        {
            try
            {
                while (true)
                {
                    var line = Console.In.ReadLine();
                    if (line is null)
                    {
                        // stdin closed -> parent went away -> also shut down.
                        StartupManager.ShutdownAll();
                        return;
                    }

                    if (line.Trim().Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                    {
                        StartupManager.ShutdownAll();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Stdin shutdown listener stopped: {ex.Message}");
            }
        })
        {
            IsBackground = true,
            Name = "stdin-shutdown-listener",
        };
        t.Start();
    }
}