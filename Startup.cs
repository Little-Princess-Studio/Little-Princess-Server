using System;
using System.Collections.Generic;
using CommandLine;
using LPS.Core;
using LPS.Core.Debug;

namespace DistServer
{
    [Verb("startup", HelpText = "Startup with a set of path")]
    class StartUpOptions
    {
        [Option('p', "pathes", Required = false, HelpText = "Set startup config pathes.")]
        public List<string> Pathes { get; set; } = new List<string>();
    }

    [Verb("bydefault", HelpText = "Startup by default")]
    class ByDefaultOptions
    {
    }

    [Verb("subproc", HelpText = "Startup sub process")]
    public class SubProcOptions
    {
        [Option("confpath", Required = false, HelpText = "Set up the child process file path")]
        public string ConfPath { get; set; } = String.Empty;

        [Option("childname", Required = false, HelpText = "Set up the child process name in conf")]
        public string ChildName { get; set; } = String.Empty;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<StartUpOptions, ByDefaultOptions, SubProcOptions>(args)
                    .MapResult(
                        (StartUpOptions opts) => { 
                            Logger.Init("startup");
                            Logger.Info("Start up with config files");
                            foreach (var path in opts.Pathes)
                            {
                                Logger.Info($"Parsing Config {path}");
                                StartupManager.FromConfig(path);
                            }
                            return true;
                        },
                        (ByDefaultOptions opts) => {
                            Logger.Init("startup");
                            Logger.Info("Start up by default");
                            StartupByDefault();
                            return true;
                        },
                        (SubProcOptions opts) => {
                            Logger.Init(opts.ChildName);
                            Logger.Info($"start {opts.ChildName}");
                            return true;
                        },
                        errs => {
                            Logger.Warn("Wrong cmd params");
                            return false;
                        }
                    );
        }

        private static void StartupByDefault()
        {
            StartupManager.FromConfig("Config/host0/hostmanager.conf.json");
            StartupManager.FromConfig("Config/host0/gate.conf.json");
            StartupManager.FromConfig("Config/host0/server.conf.json");
            StartupManager.FromConfig("Config/host0/dbmanager.conf.json");
        }
    }
}
