// -----------------------------------------------------------------------
// <copyright file="AutoCompleteConsole.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Console;

using Mono.Terminal;
using Logger = LPS.Common.Debug.Logger;

/// <summary>
/// Console with auto completing.
/// </summary>
public static class AutoCompleteConsole
{
    private static LineEditor? lineEditor;

    /// <summary>
    /// Init console.
    /// </summary>
    public static void Init()
    {
        lineEditor = new LineEditor("cmd")
        {
            HeuristicsMode = "csharp",
        };

        lineEditor.AutoCompleteEvent += (text, _) =>
        {
            var cmdArray = text.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (cmdArray.Length > 0)
            {
                var cmdName = cmdArray[0];
                var (cmdNames, _) = CommandParser.FindSuggestions(cmdName);

                lineEditor.TabAtStartCompletes = true;
                return new LineEditor.Completion(
                    cmdName,
                    cmdNames.Select(cmd => cmd.Substring(cmdName
                        .Length)).ToArray());
            }

            return new LineEditor.Completion(string.Empty, Array.Empty<string>());
        };
    }

    /// <summary>
    /// Console loop.
    /// </summary>
    public static void Loop()
    {
        string s;
        while ((s = lineEditor!.Edit("cmd> ", string.Empty)) != null)
        {
            if (s.Trim() == string.Empty)
            {
                continue;
            }

            if (s == "exit")
            {
                break;
            }

            try
            {
                CommandParser.Dispatch(s);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
        }

        Logger.Info("Bye.");
    }
}