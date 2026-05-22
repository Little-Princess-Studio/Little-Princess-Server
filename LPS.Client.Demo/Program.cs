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

        StartUpManager.Init(
            "127.0.0.1",
            11001,
            "LPS.Client.Demo.Entity",
            "LPS.Client.Demo.Entity.RpcProperty",
            "LPS.Client.Demo.Entity.RpcStub",
            () => ClientGlobal.ShadowClientEntity,
            entity => ClientGlobal.ShadowClientEntity = entity);

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