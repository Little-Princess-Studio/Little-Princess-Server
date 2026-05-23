// -----------------------------------------------------------------------
// <copyright file="ConsoleCommands.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Console;

using System.Security.Cryptography;
using System.Text;
using LPS.Client.Console;
using LPS.Client.Demo.Entity;
using LPS.Common.Debug;
using LPS.Common.Demo.Rpc;
using LPS.Common.Rpc.InnerMessages;
using Client = LPS.Client.Client;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Console commands.
/// </summary>
public static class ConsoleCommands
{
    /// <summary>
    /// Command for echo.
    /// </summary>
    /// <param name="message">Message for echo.</param>
    [ConsoleCommand("echo")]
    public static void Echo(string message)
    {
        Logger.Info($"echo: {message}");
    }

    /// <summary>
    /// Send authority message.
    /// </summary>
    [ConsoleCommand("send.authority")]
    public static void SendAuthority()
    {
        const string message = "authority-content";

        var rsa = RSA.Create();
        var pem = File.ReadAllText("./Config/demo.pub").ToCharArray();
        rsa.ImportFromPem(pem);

        var byteData = Encoding.UTF8.GetBytes(message);
        var encryptedData = Convert.ToBase64String(rsa.Encrypt(byteData, RSAEncryptionPadding.Pkcs1));

        var authMsg = new Authentication
        {
            Content = message,
            Ciphertext = encryptedData,
        };

        Client.Instance.Send(authMsg);
    }

    /// <summary>
    /// Send echo RPC to server.
    /// </summary>
    [ConsoleCommand("send.echo")]
    public static async void Echo()
    {
        var startTime = new TimeSpan(System.DateTime.Now.Ticks);
        for (int i = 0; i < 10; ++i)
        {
            var start = new TimeSpan(System.DateTime.Now.Ticks);
            var res = await ClientGlobal.ShadowClientEntity
                .Server
                .Call<string>("Echo", $"Hello, LPS, times {i}");

            var end = new TimeSpan(System.DateTime.Now.Ticks);

            Logger.Debug($"call res {res}, latancy: {(end - start).TotalMilliseconds} ms");

            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// Benchmark: send N echo RPCs sequentially, print p50/p90/p99/max plus
    /// success/failure counts so we can compare TCP vs KCP under net impairment.
    /// Output line is parseable - one trailing JSON object so the test harness
    /// can capture results without scraping logs.
    /// </summary>
    /// <param name="countStr">Number of echo round-trips. Default 100.</param>
    /// <param name="timeoutMsStr">Per-RPC timeout in ms. Default 5000.</param>
    [ConsoleCommand("send.bench")]
    public static async void Bench(string countStr = "100", string timeoutMsStr = "5000")
    {
        if (!int.TryParse(countStr, out var count))
        {
            count = 100;
        }

        if (!int.TryParse(timeoutMsStr, out var timeoutMs))
        {
            timeoutMs = 5000;
        }

        Logger.Info($"[bench] start count={count} timeoutMs={timeoutMs}");
        var latencies = new System.Collections.Generic.List<double>(count);
        var ok = 0;
        var fail = 0;
        var totalStart = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < count; ++i)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var rpcTask = ClientGlobal.ShadowClientEntity
                    .Server
                    .Call<string>("Echo", $"bench-{i}");
                var winner = await Task.WhenAny(rpcTask, Task.Delay(timeoutMs));
                if (winner != rpcTask)
                {
                    fail++;
                    Logger.Warn($"[bench] timeout #{i}");
                    continue;
                }

                await rpcTask;
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                sw.Stop();
                Logger.Warn($"[bench] failed #{i}: {ex.Message}");
            }
        }

        totalStart.Stop();
        latencies.Sort();

        static double Percentile(System.Collections.Generic.List<double> sorted, double p)
        {
            if (sorted.Count == 0)
            {
                return 0;
            }

            var idx = (int)Math.Ceiling((p / 100.0) * sorted.Count) - 1;
            idx = Math.Max(0, Math.Min(idx, sorted.Count - 1));
            return sorted[idx];
        }

        var mean = latencies.Count > 0 ? latencies.Average() : 0;
        var p50 = Percentile(latencies, 50);
        var p90 = Percentile(latencies, 90);
        var p99 = Percentile(latencies, 99);
        var max = latencies.Count > 0 ? latencies[^1] : 0;
        var min = latencies.Count > 0 ? latencies[0] : 0;

        // Single-line JSON sentinel for harness scraping.
        var json = new Newtonsoft.Json.Linq.JObject
        {
            ["bench"] = "echo",
            ["count"] = count,
            ["ok"] = ok,
            ["fail"] = fail,
            ["totalMs"] = totalStart.Elapsed.TotalMilliseconds,
            ["min"] = min,
            ["mean"] = mean,
            ["p50"] = p50,
            ["p90"] = p90,
            ["p99"] = p99,
            ["max"] = max,
        };
        Logger.Info($"[bench-result] {json.ToString(Newtonsoft.Json.Formatting.None)}");

        // Also write result to a side file so the harness can capture it
        // without scraping the stdout log (which is buffered by Start-Process
        // RedirectStandardOutput on Windows and may not flush before exit).
        try
        {
            var path = System.Environment.GetEnvironmentVariable("LPS_BENCH_RESULT_FILE");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[bench] failed to write result file: {ex.Message}");
        }

        // Force flush + exit so the harness doesn't wait for the worst-case
        // Thread.Sleep timeout in Program.Main.
        System.Console.Out.Flush();
        System.Environment.Exit(0);
    }

    /// <summary>
    /// Do local property check.
    /// </summary>
    [ConsoleCommand("local.check_untrusted_property")]
    public static void CheckUntrustedProperty()
    {
        var untrusted = ClientGlobal.ShadowClientEntity as Untrusted;
        var list = untrusted!.TestRpcProp.Val;
        Logger.Debug($"TestRpcProp: {string.Join(',', list)}");
        Logger.Debug($"TestRpcPlaintPropStr: {untrusted?.TestRpcPlaintPropStr.Val}");
    }

    /// <summary>
    /// Do local property check.
    /// </summary>
    [ConsoleCommand("local.check_player_property")]
    public static void CheckPlayerProperty()
    {
        var player = (ClientGlobal.ShadowClientEntity as Player)!;
        Logger.Debug($"Name: {player.Name.Val}");
    }

    /// <summary>
    /// Prints the components of the player entity to the console.
    /// </summary>
    [ConsoleCommand("local.print_player_components")]
    public static void PrintPlayerComponents()
    {
        var player = (ClientGlobal.ShadowClientEntity as Player)!;
        player.PrintComponents().AsTask().Wait();
    }

    /// <summary>
    /// Send property change require RPC.
    /// </summary>
    /// <param name="prop">Content to change.</param>
    [ConsoleCommand("send.change_prop")]
    public static async void ChangeProp(string prop)
    {
        var untrusted = (ClientGlobal.ShadowClientEntity as Untrusted)!;
        await untrusted.ChangeProp(prop);
        Logger.Debug($"Call to change prop");
    }

    /// <summary>
    /// Help command.
    /// </summary>
    [ConsoleCommand("help")]
    public static void Help()
    {
        var (_, cmdDetails) = CommandParser.FindSuggestions(string.Empty);

        int cnt = cmdDetails.Length;
        for (int i = 0; i < cnt; i++)
        {
            System.Console.WriteLine($"{string.Join(',', cmdDetails[i])}");
        }
    }

    /// <summary>
    /// Send transfer request.
    /// </summary>
    /// <param name="id">Entity mailbox Id.</param>
    /// <param name="ip">Entity mailbox Ip.</param>
    /// <param name="port">Entity mailbox port.</param>
    /// <param name="hostNum">Entity mailbox hostnum.</param>
    [ConsoleCommand("send.transfer")]
    public static void Transfer(string id, string ip, int port, int hostNum)
    {
        var cellMailBox = new MailBox(id, ip, port, hostNum);

        ClientGlobal.ShadowClientEntity.Server.Notify(
            "TransferIntoCell",
            cellMailBox,
            string.Empty);
    }

    /// <summary>
    /// Try to login.
    /// </summary>
    [ConsoleCommand("send.login")]
    public static async void LogIn()
    {
        await (ClientGlobal.ShadowClientEntity as Untrusted)!.Login();
        Logger.Debug($"Start login...");
    }

    /// <summary>
    /// Ping.
    /// </summary>
    /// <param name="content">Ping content.</param>
    [ConsoleCommand("send.player_ping")]
    public static async void Ping(string content)
    {
        try
        {
            if (ClientGlobal.ShadowClientEntity is not Player player)
            {
                Logger.Warn(
                    $"[send.player_ping] current entity is {ClientGlobal.ShadowClientEntity?.GetType().Name ?? "null"}, expected Player. Did login complete?");
                return;
            }

            await player.Ping(content);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[send.player_ping] failed");
        }
    }

    /// <summary>
    /// QA-only: drive the server-side shadow entity flow.
    /// Server-side CreateShadowEntity + mutate ori property.
    /// </summary>
    /// <param name="newName">New Name value to publish via shadow path.</param>
    [ConsoleCommand("send.debug_shadow")]
    public static async void DebugShadow(string newName)
    {
        try
        {
            // Login is async; this command may race with the login transition.
            // Poll up to 10s for the shadow entity to become a Player.
            var deadline = DateTime.UtcNow.AddSeconds(10);
            Player? player = null;
            while (DateTime.UtcNow < deadline)
            {
                player = ClientGlobal.ShadowClientEntity as Player;
                if (player is not null)
                {
                    break;
                }

                await Task.Delay(200);
            }

            if (player is null)
            {
                Logger.Warn(
                    $"[send.debug_shadow] current entity is {ClientGlobal.ShadowClientEntity?.GetType().Name ?? "null"} after 10s wait, expected Player.");
                Environment.Exit(1);
                return;
            }

            var res = await player.DebugCreateShadowAndMutate(newName);
            Logger.Info($"[send.debug_shadow] result: {res}");

            var resultFile = Environment.GetEnvironmentVariable("LPS_DEBUG_SHADOW_RESULT_FILE");
            if (!string.IsNullOrEmpty(resultFile))
            {
                await System.IO.File.WriteAllTextAsync(resultFile, res);
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[send.debug_shadow] failed");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Call service echo.
    /// </summary>
    /// <param name="msg">Echo message.</param>
    [ConsoleCommand("send.call_service_echo")]
    public static async void CallServiceEcho(string msg)
    {
        await (ClientGlobal.ShadowClientEntity as Untrusted)!.CallServiceEcho(msg);
    }

    /// <summary>
    /// Calls the service echo with callback from service.
    /// </summary>
    /// <param name="msg">The message to be echoed.</param>
    [ConsoleCommand("send.call_service_echo_with_callback")]
    public static async void CallServiceEchoWithCallBack(string msg)
    {
        await (ClientGlobal.ShadowClientEntity as Untrusted)!.CallServiceEchoWithCallBack(msg);
    }
}