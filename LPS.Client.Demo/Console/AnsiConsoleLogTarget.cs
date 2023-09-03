// -----------------------------------------------------------------------
// <copyright file="AnsiConsoleLogTarget.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Console;

using NLog;
using NLog.Layouts;
using NLog.Targets;
using Spectre.Console;

/// <summary>
/// Represents a log target that writes log events to the console using ANSI escape codes for color and style.
/// </summary>
[Target("ansiConsole")]
public sealed class AnsiConsoleLogTarget : TargetWithLayout
{
    /// <inheritdoc/>
    protected override void Write(LogEventInfo logEvent)
    {
        string logMessage = this.Layout.Render(logEvent);
        AnsiConsole.WriteLine(logMessage);
    }
}