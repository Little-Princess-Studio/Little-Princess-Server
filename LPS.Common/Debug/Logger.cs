// -----------------------------------------------------------------------
// <copyright file="Logger.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Debug;

using NLog;

/// <summary>
/// LPS Logger class.
/// </summary>
public static class Logger
{
    private static readonly NLog.Logger Default = LogManager.GetLogger("Default");

    /// <summary>
    /// Init logger.
    /// </summary>
    /// <param name="logFileName">File name of the logger.</param>
    public static void Init(string logFileName)
    {
        try
        {
            var path = Path.Join(Directory.GetCurrentDirectory(), "Config", "nlog.config");
            var logDirPath = Path.Join(Directory.GetCurrentDirectory(), "logs");

            LogManager.LoadConfiguration(path);
            LogManager.Configuration.Variables["logDir"] = logDirPath;
            LogManager.Configuration.Variables["fileName"] = logFileName;
        }
        catch (Exception)
        {
            Console.WriteLine("Error Initializing Logger");
            throw;
        }
    }

    /// <summary>
    /// Write info log.
    /// </summary>
    /// <param name="msg">Log message.</param>
    public static void Info(params object[] msg)
    {
        Default.Info(string.Join(string.Empty, msg));
    }

    /// <summary>
    /// Write debug log.
    /// </summary>
    /// <param name="msg">Log message.</param>
    public static void Debug(params object[] msg)
    {
        Default.Debug(string.Join(string.Empty, msg));
    }

    /// <summary>
    /// Write warn log.
    /// </summary>
    /// <param name="msg">Log message.</param>
    public static void Warn(params object[] msg)
    {
        Default.Warn(string.Join(string.Empty, msg));
    }

    /// <summary>
    /// Write error log.
    /// </summary>
    /// <param name="e">Exception object.</param>
    /// <param name="msg">Log message.</param>
    public static void Error(Exception e, params object[] msg)
    {
        Default.Error(e, string.Join(string.Empty, msg));
    }

    /// <summary>
    /// Write fatal log.
    /// </summary>
    /// <param name="e">Exception object.</param>
    /// <param name="msg">Log message.</param>
    public static void Fatal(Exception e, params object[] msg)
    {
        Default.Fatal(e, string.Join(string.Empty, msg));
    }
}