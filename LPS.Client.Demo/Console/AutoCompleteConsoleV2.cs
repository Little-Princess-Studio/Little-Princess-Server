// -----------------------------------------------------------------------
// <copyright file="AutoCompleteConsoleV2.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using LPS.Client.Demo.Console;
using Spectre.Console;

/// <summary>
/// Console with auto completing.
/// </summary>
public static class AutoCompleteConsoleV2
{
    /// <summary>
    /// Initializes the console application.
    /// </summary>
    public static void Init()
    {
    }

    /// <summary>
    /// Console loop.
    /// </summary>
    public static void Loop()
    {
        var suggestions = CommandParser.GetAllCmdNames();
        while (true)
        {
            var s = AnsiConsole.Prompt(
                new TextPrompt<string>("cmd> ")
                    .AddChoices(suggestions)
                    .ShowChoices(false)
                    .Validate(sugg => suggestions.Any(cmd => sugg.StartsWith(cmd)))
                    .InvalidChoiceMessage("Invalid cmd name. Please input again."));

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
    }
}