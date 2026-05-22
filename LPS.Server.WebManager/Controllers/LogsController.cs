// -----------------------------------------------------------------------
// <copyright file="LogsController.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.WebManager.Controllers;

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Read-only file-system view over the cluster's NLog output directory.
/// Lets the WebManager UI list per-instance log files (gate0, server0, dbmanager, ...)
/// and stream the tail of any one of them without shelling into the host.
/// </summary>
[ApiController]
[Route("api/web-manager/logs")]
public class LogsController : Controller
{
    /// <summary>Per-name file picker pattern: '<name>-YYYY-MM-DD.log'.</summary>
    private static readonly Regex LogNamePattern = new(
        @"^(?<name>[^/\\]+?)-(?<date>\d{4}-\d{2}-\d{2})\.log$",
        RegexOptions.Compiled);

    /// <summary>Hard cap on how many tail lines a single request may ask for.</summary>
    private const int MaxTailLines = 5000;

    /// <summary>How many bytes from the end of file we are willing to scan
    /// to find <see cref="MaxTailLines"/> newlines. Keeps memory bounded
    /// regardless of file size.</summary>
    private const int MaxTailScanBytes = 4 * 1024 * 1024;

    private readonly string logsDirectory;
    private readonly ILogger<LogsController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsController"/> class.
    /// </summary>
    /// <param name="config">App config (reads <c>LpsLogsDirectory</c>).</param>
    /// <param name="logger">Logger.</param>
    public LogsController(IConfiguration config, ILogger<LogsController> logger)
    {
        var configured = config["LpsLogsDirectory"] ?? "../LPS.Server.Demo/logs";
        this.logsDirectory = Path.GetFullPath(configured);
        this.logger = logger;
    }

    /// <summary>
    /// Lists the most recent log file per instance name. Today's file is preferred,
    /// otherwise the most recently modified file for that name is returned.
    /// </summary>
    /// <returns>Per-instance metadata.</returns>
    [HttpGet("list")]
    public IActionResult ListLogFiles()
    {
        if (!Directory.Exists(this.logsDirectory))
        {
            return this.Ok(new
            {
                res = "Ok",
                logsDirectory = this.logsDirectory,
                exists = false,
                logs = Array.Empty<object>(),
            });
        }

        var perName = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new DirectoryInfo(this.logsDirectory).EnumerateFiles("*.log"))
        {
            var match = LogNamePattern.Match(file.Name);
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            if (!perName.TryGetValue(name, out var existing) || file.LastWriteTimeUtc > existing.LastWriteTimeUtc)
            {
                perName[name] = file;
            }
        }

        var logs = perName
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new
            {
                name = kv.Key,
                fileName = kv.Value.Name,
                sizeBytes = kv.Value.Length,
                lastWriteUtc = kv.Value.LastWriteTimeUtc,
            })
            .ToArray();

        return this.Ok(new
        {
            res = "Ok",
            logsDirectory = this.logsDirectory,
            exists = true,
            logs,
        });
    }

    /// <summary>
    /// Returns the last <paramref name="lines"/> lines of the most recent log file
    /// for <paramref name="name"/>. Reads the file from the end so it stays cheap
    /// even on multi-MB logs.
    /// </summary>
    /// <param name="name">Instance name (e.g. 'gate0', 'server1', 'dbmanager').</param>
    /// <param name="lines">Number of trailing lines to return. Clamped to <see cref="MaxTailLines"/>.</param>
    /// <returns>Tail content.</returns>
    [HttpGet("tail")]
    public IActionResult Tail([FromQuery] string name, [FromQuery] int lines = 200)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return this.BadRequest(new { res = "Error", error = "name is required" });
        }

        // Prevent path traversal: the name must match the [name] part of '<name>-YYYY-MM-DD.log'.
        if (name.Contains('/') || name.Contains('\\') || name.Contains("..", StringComparison.Ordinal))
        {
            return this.BadRequest(new { res = "Error", error = "invalid name" });
        }

        if (!Directory.Exists(this.logsDirectory))
        {
            return this.NotFound(new { res = "Error", error = $"logsDirectory does not exist: {this.logsDirectory}" });
        }

        var clampedLines = Math.Clamp(lines, 1, MaxTailLines);

        var matching = new DirectoryInfo(this.logsDirectory)
            .EnumerateFiles($"{name}-*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (matching is null)
        {
            return this.NotFound(new { res = "Error", error = $"no log file for name '{name}'" });
        }

        var tail = ReadLastLines(matching.FullName, clampedLines, MaxTailScanBytes);

        return this.Ok(new
        {
            res = "Ok",
            name,
            fileName = matching.Name,
            totalSize = matching.Length,
            returnedLines = tail.Length,
            truncated = tail.Length == clampedLines,
            lines = tail,
        });
    }

    /// <summary>
    /// Read up to <paramref name="maxLines"/> trailing lines from <paramref name="path"/>,
    /// scanning at most <paramref name="maxScanBytes"/> bytes back from EOF so the worst
    /// case stays bounded regardless of how big the file grew.
    /// </summary>
    private static string[] ReadLastLines(string path, int maxLines, int maxScanBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var fileLen = stream.Length;
        var toRead = (int)Math.Min(fileLen, maxScanBytes);
        stream.Seek(fileLen - toRead, SeekOrigin.Begin);
        var buf = new byte[toRead];
        var read = 0;
        while (read < toRead)
        {
            var n = stream.Read(buf, read, toRead - read);
            if (n <= 0)
            {
                break;
            }

            read += n;
        }

        var text = System.Text.Encoding.UTF8.GetString(buf, 0, read);

        // If we did not start at byte 0, the first line is probably a partial
        // line, drop it.
        if (toRead < fileLen)
        {
            var nl = text.IndexOf('\n');
            if (nl >= 0)
            {
                text = text[(nl + 1)..];
            }
        }

        var all = text.Split('\n');

        // Drop trailing empty line if the file ended with \n.
        if (all.Length > 0 && all[^1].Length == 0)
        {
            all = all[..^1];
        }

        var startIdx = Math.Max(0, all.Length - maxLines);
        var slice = new string[all.Length - startIdx];
        for (var i = 0; i < slice.Length; i++)
        {
            slice[i] = all[startIdx + i].TrimEnd('\r');
        }

        return slice;
    }
}
