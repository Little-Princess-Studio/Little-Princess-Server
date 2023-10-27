// -----------------------------------------------------------------------
// <copyright file="Startup.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo;

using System;
using System.Collections.Generic;
using CommandLine;
using Common.Debug;

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
        public bool HotReload { get; set; }
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
    }
#pragma warning restore CS8618

    /// <summary>
    /// Startup entry.
    /// </summary>
    /// <param name="args">Args.</param>
    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<StartUpOptions, ByDefaultOptions, SubProcOptions>(args)
            .MapResult(
                (StartUpOptions opts) =>
                {
                    Logger.Init("startup");
                    Logger.Info("Start up with config files");
                    foreach (var path in opts.Pathes)
                    {
                        Logger.Info($"Parsing Config {path}");
                        StartupManager.FromConfig(path, opts.HotReload);
                    }

                    Logger.Info("Start up succ");
                    StartupManager.WatchAllSubProcesses();
                    return true;
                },
                (ByDefaultOptions opts) =>
                {
                    Logger.Init("startup");
                    Logger.Info("Start up by default");
                    StartupByDefault(opts.HotReload);
                    Logger.Info("Start up succ");
                    return true;
                },
                (SubProcOptions opts) =>
                {
                    Logger.Init(opts.ChildName);
                    Logger.Info($"start {opts.ChildName} {opts.Type} {opts.ConfPath}");

                    try
                    {
                        StartupManager.StartUp(opts.Type, opts.ChildName, opts.ConfPath);
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
        StartupManager.FromConfig("Config/host0/hostmanager.conf.json", hotreload);
        StartupManager.FromConfig("Config/host0/gate.conf.json", hotreload);
        StartupManager.FromConfig("Config/host0/server.conf.json", hotreload);
        StartupManager.FromConfig("Config/host0/dbmanager.conf.json", hotreload);
        StartupManager.FromConfig("Config/host0/service.conf.json", hotreload);
        StartupManager.WatchAllSubProcesses();
    }
}