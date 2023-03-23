// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo;

using Google.Protobuf;
using LPS.Client.Demo.Console;
using LPS.Client.Rpc;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.InnerMessages.ProtobufDefs;
using Client = LPS.Client.Client;

/// <summary>
/// Client entry class.
/// </summary>
public class Program
{
    private static readonly Random Random = new Random();

    /// <summary>
    /// Client Entry.
    /// </summary>
    /// <param name="args">Entry args.</param>
    public static void Main(string[] args)
    {
        Logger.Init("client");
        RpcProtobufDefs.Init();
        RpcHelper.ScanRpcMethods("LPS.Client.Entity");
        RpcHelper.ScanRpcPropertyContainer("LPS.Client.Entity.RpcProperties");
        CommandParser.ScanCommands("LPS.Client");

        Client.Instance.Init("127.0.0.1", 11001);

        // Client.Instance.Init("52.175.74.209", 11001);
        Client.Instance.RegisterMessageHandler(PackageType.ClientCreateEntity, HandleClientCreateEntity);
        Client.Instance.RegisterMessageHandler(PackageType.EntityRpc, HandleEntityRpc);
        Client.Instance.RegisterMessageHandler(PackageType.PropertyFullSync, HandlePropertyFullSync);
        Client.Instance.RegisterMessageHandler(PackageType.PropertySyncCommandList, HandlePropertySyncCommandList);

        Client.Instance.Start();

        AutoCompleteConsole.Init();
        AutoCompleteConsole.Loop();

        Client.Instance.Stop();
        Client.Instance.WaitForExit();
    }

    private static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    private static void HandlePropertySyncCommandList(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var syncCommandList = (msg as PropertySyncCommandList) !;

        Logger.Debug($"[HandlePropertySyncCommandList] {msg}");

        ClientGlobal.ShadowClientEntity.ApplySyncCommandList(syncCommandList);
    }

    private static void HandleEntityRpc(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var entityRpc = (EntityRpc)msg;

        // Logger.Info($"rpc msg from server {entityRpc}");
        RpcHelper.CallLocalEntity(ClientGlobal.ShadowClientEntity, entityRpc);
    }

    private static void HandleClientCreateEntity(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var clientCreateEntity = (ClientCreateEntity)msg;

        Logger.Info(
            $"Client create entity: {clientCreateEntity.EntityClassName} " +
            $"{clientCreateEntity.ServerClientMailBox}");

        var shadowEntity = RpcClientHelper.CreateClientEntity(clientCreateEntity.EntityClassName);
        shadowEntity.OnSend = rpc => { Client.Instance.Send(rpc); };
        shadowEntity.MailBox = RpcHelper.PbMailBoxToRpcMailBox(clientCreateEntity.ServerClientMailBox);
        shadowEntity.BindServerMailBox();

        ClientGlobal.ShadowClientEntity = shadowEntity;
        Logger.Info($"{shadowEntity} created success.");

        var requireFullSync = new RequirePropertyFullSync()
        {
            EntityId = shadowEntity.MailBox.Id,
        };

        Client.Instance.Send(requireFullSync);
        Logger.Info($"require full property sync");
    }

    private static void HandlePropertyFullSync(object arg)
    {
        var (msg, _, _) = ((IMessage, Connection, uint))arg;
        var propertyFullSyncMsg = (PropertyFullSync)msg;

        if (propertyFullSyncMsg.EntityId != ClientGlobal.ShadowClientEntity.MailBox.Id)
        {
            throw new Exception(
                $"Invalid property full sync {propertyFullSyncMsg.EntityId} {ClientGlobal.ShadowClientEntity.MailBox.Id}");
        }

        Logger.Info("On Full Sync Msg");
        ClientGlobal.ShadowClientEntity.FromSyncContent(propertyFullSyncMsg.PropertyTree);

        var ack = new PropertyFullSyncAck
        {
            EntityId = ClientGlobal.ShadowClientEntity.MailBox.Id,
        };
        Client.Instance.Send(ack);
    }
}