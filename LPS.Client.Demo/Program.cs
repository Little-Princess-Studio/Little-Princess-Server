// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo;

using LPS.Client.Demo.Console;

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
            () => ClientGlobal.ShadowClientEntity,
            entity => ClientGlobal.ShadowClientEntity = entity);

        StartUpManager.StartClient();

        AutoCompleteConsole.Init();
        AutoCompleteConsole.Loop();

        StartUpManager.StopClient();
    }

    private static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }
}