// -----------------------------------------------------------------------
// <copyright file="Supervisor.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo;

using System;
using System.Linq;
using LPS.Common.Debug;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

/// <summary>
/// Embedded HTTP supervisor co-located with <see cref="Startup"/>. WebManager
/// posts cluster-level and per-instance lifecycle requests here; we hand off
/// to <see cref="StartupManager"/> which owns the subprocess registry.
/// <para>
/// The supervisor intentionally lives in the launcher process (rather than
/// in WebManager itself or in <c>LPS.Server</c>) because only the launcher
/// has authority over the subprocess lifecycle. WebManager going down must
/// not take the cluster with it.
/// </para>
/// </summary>
public static class Supervisor
{
    /// <summary>Port the supervisor binds to. Chosen to avoid clashes with
    /// WebManager (7087/7088) and HostManager (10001).</summary>
    public const int DefaultPort = 7090;

    /// <summary>
    /// Build and start the supervisor on a background thread so the
    /// launcher's existing <see cref="StartupManager.WatchAllSubProcesses"/>
    /// loop keeps the foreground.
    /// </summary>
    public static void Start(int port = DefaultPort)
    {
        var thread = new System.Threading.Thread(() => Run(port))
        {
            IsBackground = true,
            Name = "supervisor-http",
        };
        thread.Start();
    }

    private static void Run(int port)
    {
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(port));

            // Mirror WebManager's blanket dev CORS so the React dev server
            // (port 3000) can call us directly when running outside the
            // ASP.NET proxy.
            builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
                .AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            // Drown out the Kestrel/Hosting noise (matches the rest of the
            // cluster which uses our own LPS.Common.Debug.Logger).
            builder.Logging.ClearProviders();

            var app = builder.Build();
            app.UseCors();
            MapEndpoints(app);

            Logger.Info($"[supervisor] HTTP listening on http://localhost:{port}");
            app.Run();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[supervisor] crashed.");
        }
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/supervisor/status", () =>
        {
            var rows = StartupManager.GetSubProcessStatus()
                .Select(s => new JObject
                {
                    ["name"] = s.Name,
                    ["type"] = s.Type,
                    ["alive"] = s.Alive,
                    ["pid"] = s.Pid,
                    ["hasExited"] = s.HasExited,
                })
                .ToArray();
            var body = new JObject
            {
                ["res"] = "Ok",
                ["instances"] = new JArray(rows.Cast<object>().ToArray()),
            };
            return Results.Content(body.ToString(), "application/json");
        });

        app.MapPost("/supervisor/cluster/start", () =>
        {
            var started = StartupManager.StartAllInstances();
            Logger.Info($"[supervisor] cluster/start started {started} instances.");
            return Ok(new JObject { ["startedCount"] = started });
        });

        app.MapPost("/supervisor/cluster/stop", () =>
        {
            Logger.Info("[supervisor] cluster/stop");
            StartupManager.StopAllInstances();
            return Ok(new JObject { ["stopped"] = true });
        });

        app.MapPost("/supervisor/cluster/restart", () =>
        {
            Logger.Info("[supervisor] cluster/restart");
            StartupManager.StopAllInstances();

            // Brief settle so port bindings clear before re-spawn.
            System.Threading.Thread.Sleep(1500);
            var started = StartupManager.StartAllInstances();
            return Ok(new JObject { ["startedCount"] = started });
        });

        app.MapPost("/supervisor/instance/{name}/start", (string name) =>
        {
            var ok = StartupManager.StartInstance(name);
            return Ok(new JObject { ["name"] = name, ["started"] = ok });
        });

        app.MapPost("/supervisor/instance/{name}/stop", (string name) =>
        {
            var ok = StartupManager.StopInstance(name);
            return Ok(new JObject { ["name"] = name, ["stopped"] = ok });
        });

        app.MapPost("/supervisor/instance/{name}/restart", (string name) =>
        {
            var ok = StartupManager.RestartInstance(name);
            return Ok(new JObject { ["name"] = name, ["restarted"] = ok });
        });
    }

    private static IResult Ok(JObject payload)
    {
        payload["res"] = "Ok";
        return Results.Content(payload.ToString(), "application/json");
    }
}
