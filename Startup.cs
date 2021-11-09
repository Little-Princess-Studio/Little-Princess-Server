using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using CommandLine;
using LPS.Core;

namespace DistServer
{
    public class Options
    {
        [Option('p', "pathes", Required = false, HelpText = "Set startup config pathes.")]
        public List<string> Pathes { get; set; } = new List<string>();
        [Option('b', "bydefault", Required = false, HelpText = "Use default config to startup.")]
        public bool ByDefault { get; set; } = false;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    if (o.ByDefault)
                    {
                        StartupByDefault();
                    }
                    else
                    {
                        foreach (var path in o.Pathes)
                        {
                            StartupManager.FromConfig(path);
                        }
                    }
                })
                .WithNotParsed((Action<IEnumerable<Error>>)(e =>
                {
                    Console.WriteLine("Wrong cmd params, startup with default demo config ...");

                    StartupByDefault();
                }));
        }

        private static void StartupByDefault()
        {
            StartupManager.FromConfig("Config/hostmanager.conf.json");
            StartupManager.FromConfig("Config/gate.conf.json");
            StartupManager.FromConfig("Config/server.conf.json");
            StartupManager.FromConfig("Config/dbmanager.conf.json");
        }
    }
}
