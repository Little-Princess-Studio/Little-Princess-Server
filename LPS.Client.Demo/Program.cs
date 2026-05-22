// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using LPS.Client.Demo.Console;
using LPS.Common.Debug;

/// <summary>
/// Client entry class.
/// </summary>
public static class Program
{
    private static readonly Random Random = new Random();

    /// <summary>
    /// Client Entry.
    /// </summary>
    /// <param name="args">Entry args.</param>
    public static void Main(string[] args)
    {
        CommandParser.ScanCommands("LPS.Client.Demo");

        // Parse --transport tcp|kcp + optional --port override so the demo
        // can validate either gate listener without recompiling.
        var transport = ClientTransport.Tcp;
        var port = 11001;
        var transportIdx = Array.IndexOf(args, "--transport");
        if (transportIdx >= 0 && transportIdx < args.Length - 1)
        {
            var t = args[transportIdx + 1].ToLowerInvariant();
            if (t == "kcp")
            {
                transport = ClientTransport.Kcp;
                port = 11002;
            }
        }

        var portIdx = Array.IndexOf(args, "--port");
        if (portIdx >= 0 && portIdx < args.Length - 1
            && int.TryParse(args[portIdx + 1], out var parsed))
        {
            port = parsed;
        }

        Logger.Info($"[Client] transport={transport} port={port}");

        StartUpManager.Init(
            "127.0.0.1",
            port,
            "LPS.Client.Demo.Entity",
            "LPS.Client.Demo.Entity.RpcProperty",
            "LPS.Client.Demo.Entity.RpcStub",
            () => ClientGlobal.ShadowClientEntity,
            entity => ClientGlobal.ShadowClientEntity = entity,
            transport);

        StartUpManager.StartClient();

        var runIndex = Array.IndexOf(args, "--run");
        var scenarioIndex = Array.IndexOf(args, "--scenario");

        if (runIndex != -1 && runIndex < args.Length - 1)
        {
            var cmds = args[(runIndex + 1)..];
            foreach (var cmd in cmds)
            {
                Logger.Info($"[AutoDebug] Executing: {cmd}");
                try
                {
                    CommandParser.Dispatch(cmd);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"[AutoDebug] Failed to execute {cmd}");
                }

                Thread.Sleep(1500);
            }
        }
        else if (scenarioIndex != -1 && scenarioIndex < args.Length - 1)
        {
            var scenarioPath = args[scenarioIndex + 1];
            Logger.Info($"[AutoDebug] Running scenario from: {scenarioPath}");
            try
            {
                var content = File.ReadAllText(scenarioPath);
                using var doc = JsonDocument.Parse(content);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var cmd = element.GetProperty("command").GetString()!;
                    var waitMs = element.TryGetProperty("waitMs", out var waitProp) ? waitProp.GetInt32() : 1500;

                    Logger.Info($"[AutoDebug] Scenario Executing: {cmd}");
                    try
                    {
                        CommandParser.Dispatch(cmd);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"[AutoDebug] Scenario failed to execute {cmd}");
                    }

                    Thread.Sleep(waitMs);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[AutoDebug] Scenario execution error");
            }
        }
        else
        {
            AutoCompleteConsoleV2.Init();
            AutoCompleteConsoleV2.Loop();
        }

        StartUpManager.StopClient();
    }

    private static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }
}