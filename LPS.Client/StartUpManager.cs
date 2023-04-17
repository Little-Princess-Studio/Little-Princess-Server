// -----------------------------------------------------------------------
// <copyright file="StartUpManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client;

using Google.Protobuf;
using LPS.Client.Entity;
using LPS.Client.Rpc;
using LPS.Common.Debug;
using LPS.Common.Rpc;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.InnerMessages.ProtobufDefs;

/// <summary>
/// Start up manager for client.
/// </summary>
public class StartUpManager
{
    private static Action<ShadowClientEntity> createShadowEntityCallBack = null!;
    private static Func<ShadowClientEntity> getShadowEntityCallBack = null!;

    /// <summary>
    /// Init client enviroment.
    /// </summary>
    /// <param name="ip">Server ip to connect.</param>
    /// <param name="port">Port to connect.</param>
    /// <param name="entityNamespace">Costume entity class namespace to scan.</param>
    /// <param name="rpcPropertyNamespace">Costume rpc property class namespace to scan.</param>
    /// <param name="getShadowEntity">Function to get current client shadow entity.</param>
    /// <param name="onCreateShadowEntity">Callback when shadow entity created.</param>
    public static void Init(
        string ip,
        int port,
        string entityNamespace,
        string rpcPropertyNamespace,
        Func<ShadowClientEntity> getShadowEntity,
        Action<ShadowClientEntity> onCreateShadowEntity)
    {
        getShadowEntityCallBack = getShadowEntity;
        createShadowEntityCallBack = onCreateShadowEntity;

        Logger.Init("client");
        RpcProtobufDefs.Init();
        RpcHelper.ScanRpcMethods("LPS.Client.Entity");
        RpcHelper.ScanRpcMethods(entityNamespace);
        RpcHelper.ScanRpcPropertyContainer(rpcPropertyNamespace);

        Client.Instance.Init(ip, port);

        Client.Instance.RegisterMessageHandler(PackageType.ClientCreateEntity, HandleClientCreateEntity);
        Client.Instance.RegisterMessageHandler(PackageType.EntityRpc, HandleEntityRpc);
        Client.Instance.RegisterMessageHandler(PackageType.PropertyFullSync, HandlePropertyFullSync);
        Client.Instance.RegisterMessageHandler(PackageType.PropertySyncCommandList, HandlePropertySyncCommandList);
    }

    /// <summary>
    /// Start client.
    /// </summary>
    public static void StartClient() => Client.Instance.Start();

    /// <summary>
    /// Stop client and wait for exiting.
    /// </summary>
    public static void StopClient()
    {
        Client.Instance.Stop();
        Client.Instance.WaitForExit();
    }

    private static void HandlePropertySyncCommandList((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var syncCommandList = (msg as PropertySyncCommandList)!;

        Logger.Debug($"[HandlePropertySyncCommandList] {msg}");

        getShadowEntityCallBack().ApplySyncCommandList(syncCommandList);
    }

    private static void HandleEntityRpc((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var entityRpc = (EntityRpc)msg;

        // Logger.Info($"rpc msg from server {entityRpc}");
        RpcHelper.CallLocalEntity(getShadowEntityCallBack(), entityRpc);
    }

    private static void HandleClientCreateEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var clientCreateEntity = (ClientCreateEntity)msg;

        Logger.Info(
            $"Client create entity: {clientCreateEntity.EntityClassName} " +
            $"{clientCreateEntity.ServerClientMailBox}");

        var shadowEntity = RpcClientHelper.CreateClientEntity(clientCreateEntity.EntityClassName);
        shadowEntity.OnSend = rpc => { Client.Instance.Send(rpc); };
        shadowEntity.MailBox = RpcHelper.PbMailBoxToRpcMailBox(clientCreateEntity.ServerClientMailBox);
        shadowEntity.BindServerMailBox();

        createShadowEntityCallBack(shadowEntity);

        Logger.Info($"{shadowEntity} created success.");

        RpcClientHelper.RequirePropertyFullSync(shadowEntity.MailBox.Id);
    }

    private static void HandlePropertyFullSync((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var propertyFullSyncMsg = (PropertyFullSync)msg;

        var shadowEntity = getShadowEntityCallBack();

        if (propertyFullSyncMsg.EntityId != shadowEntity.MailBox.Id)
        {
            throw new Exception(
                $"Invalid property full sync {propertyFullSyncMsg.EntityId} {shadowEntity.MailBox.Id}");
        }

        Logger.Info("On Full Sync Msg");
        shadowEntity.FromSyncContent(propertyFullSyncMsg.PropertyTree);

        var ack = new PropertyFullSyncAck
        {
            EntityId = shadowEntity.MailBox.Id,
        };
        Client.Instance.Send(ack);
    }
}