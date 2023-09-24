// -----------------------------------------------------------------------
// <copyright file="AutoCompleteConsoleV2.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using LPS.Client.Demo.Console;

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
        ReadLine.AutoCompletionHandler = new AutoCompletionHandler();

        while (true)
        {
            var s = ReadLine.Read("cmd>");

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

    private class AutoCompletionHandler : IAutoCompleteHandler
    {
        public char[] Separators { get; set; } = new char[] { };

        private string[] suggestions = CommandParser.GetAllCmdNames();

        public string[] GetSuggestions(string text, int index)
        {
            return this.suggestions.Where(cmd => cmd.StartsWith(text)).Select(cmd => cmd.Substring(0)).ToArray();
        }
    }
}