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
        var player = (ClientGlobal.ShadowClientEntity as Player) !;
        Logger.Debug($"Name: {player.Name.Val}");
    }

    /// <summary>
    /// Prints the components of the player entity to the console.
    /// </summary>
    [ConsoleCommand("local.print_player_components")]
    public static void PrintPlayerComponents()
    {
        var player = (ClientGlobal.ShadowClientEntity as Player) !;
        player.PrintComponents().AsTask().Wait();
    }

    /// <summary>
    /// Send property change require RPC.
    /// </summary>
    /// <param name="prop">Content to change.</param>
    [ConsoleCommand("send.change_prop")]
    public static async void ChangeProp(string prop)
    {
        var untrusted = (ClientGlobal.ShadowClientEntity as Untrusted) !;
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
        await (ClientGlobal.ShadowClientEntity as Untrusted) !.Login();
        Logger.Debug($"Start login...");
    }

    /// <summary>
    /// Ping.
    /// </summary>
    /// <param name="content">Ping content.</param>
    [ConsoleCommand("send.player_ping")]
    public static async void Ping(string content)
    {
        await (ClientGlobal.ShadowClientEntity as Player) !.Ping(content);
    }

    /// <summary>
    /// Call service echo.
    /// </summary>
    /// <param name="msg">Echo message.</param>
    [ConsoleCommand("send.call_service_echo")]
    public static async void CallServiceEcho(string msg)
    {
        await (ClientGlobal.ShadowClientEntity as Untrusted) !.CallServiceEcho(msg);
    }

    /// <summary>
    /// Calls the service echo with callback from service.
    /// </summary>
    /// <param name="msg">The message to be echoed.</param>
    [ConsoleCommand("send.call_service_echo_with_callback")]
    public static async void CallServiceEchoWithCallBack(string msg)
    {
        await (ClientGlobal.ShadowClientEntity as Untrusted) !.CallServiceEchoWithCallBack("msg");
    }
}