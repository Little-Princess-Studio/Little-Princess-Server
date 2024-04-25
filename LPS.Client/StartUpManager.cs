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
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Start up manager for client.
/// </summary>
public static class StartUpManager
{
    private static Action<ShadowClientEntity> createShadowEntityCallBack = null!;
    private static Func<ShadowClientEntity> getShadowEntityCallBack = null!;

    /// <summary>
    /// Init client environment.
    /// </summary>
    /// <param name="ip">Server ip to connect.</param>
    /// <param name="port">Port to connect.</param>
    /// <param name="entityNamespace">Costume entity class namespace to scan.</param>
    /// <param name="rpcPropertyNamespace">Costume rpc property class namespace to scan.</param>
    /// <param name="rpcStubInterfaceNamespace">The namespaces to scan rpc stub interfaces.</param>
    /// <param name="getShadowEntity">Function to get current client shadow entity.</param>
    /// <param name="onCreateShadowEntity">Callback when shadow entity created.</param>
    public static void Init(
        string ip,
        int port,
        string entityNamespace,
        string rpcPropertyNamespace,
        string rpcStubInterfaceNamespace,
        Func<ShadowClientEntity> getShadowEntity,
        Action<ShadowClientEntity> onCreateShadowEntity)
    {
        getShadowEntityCallBack = getShadowEntity;
        createShadowEntityCallBack = onCreateShadowEntity;

        Logger.Init("client");
        RpcProtobufDefs.Init();
        var extraAssemblies = new System.Reflection.Assembly[] { typeof(StartUpManager).Assembly };
        RpcHelper.ScanRpcMethods(new[] { "LPS.Common.Entity", "LPS.Client.Entity", entityNamespace }, extraAssemblies);
        RpcHelper.ScanRpcPropertyContainer(rpcPropertyNamespace, extraAssemblies);
        RpcStubGeneratorManager.ScanAndBuildGenerator(
            new[] { rpcStubInterfaceNamespace },
            new[] { typeof(RpcStubForShadowClientAttribute) },
            extraAssemblies);

        Client.Instance.Init(ip, port);

        Client.Instance.RegisterMessageHandler(PackageType.ClientCreateEntity, HandleClientCreateEntity);
        Client.Instance.RegisterMessageHandler(PackageType.EntityRpc, HandleEntityRpc);
        Client.Instance.RegisterMessageHandler(PackageType.EntityRpcCallBack, HandleEntityRpcCallBack);
        Client.Instance.RegisterMessageHandler(PackageType.PropertyFullSync, HandlePropertyFullSync);
        Client.Instance.RegisterMessageHandler(PackageType.PropertySyncCommandList, HandlePropertySyncCommandList);
        Client.Instance.RegisterMessageHandler(PackageType.ComponentSync, HandleComponentSync);
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

        getShadowEntityCallBack().ApplySyncCommandList(
            syncCommandList,
            syncCommandList.IsComponentSyncMsg,
            syncCommandList.ComponentName);
    }

    private static void HandleEntityRpc((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var entityRpc = (EntityRpc)msg;

        // Logger.Info($"rpc msg from server {entityRpc}");
        RpcHelper.CallLocalEntity(getShadowEntityCallBack(), entityRpc);
    }

    private static void HandleEntityRpcCallBack((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var callBack = (EntityRpcCallBack)msg;

        var localEntity = getShadowEntityCallBack();
        localEntity.OnRpcCallBack(callBack);
    }

    private static void HandleClientCreateEntity((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var clientCreateEntity = (ClientCreateEntity)msg;

        Logger.Info(
            $"Client create entity: {clientCreateEntity.EntityClassName} " +
            $"{clientCreateEntity.ServerClientMailBox}");

        var shadowEntityTask = RpcClientHelper.CreateClientEntity(clientCreateEntity.EntityClassName);
        shadowEntityTask.ContinueWith(t =>
        {
            var shadowEntity = t.Result;
            shadowEntity.OnSendEntityRpc = rpc => { Client.Instance.Send(rpc); };
            shadowEntity.MailBox = RpcHelper.PbMailBoxToRpcMailBox(clientCreateEntity.ServerClientMailBox);
            shadowEntity.BindServerMailBox();

            createShadowEntityCallBack(shadowEntity);

            Logger.Info($"{shadowEntity} created success.");

            RpcClientHelper.RequirePropertyFullSync(shadowEntity.MailBox.Id);
        });
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
        shadowEntity.OnLoaded().ContinueWith((t) =>
        {
            if (t.Exception != null)
            {
                Logger.Error(t.Exception);
                return;
            }
        });
    }

    private static void HandleComponentSync((IMessage Message, Connection Connection, uint RpcId) arg)
    {
        var (msg, _, _) = arg;
        var componentSyncMsg = (ComponentSync)msg;

        var shadowEntity = getShadowEntityCallBack();

        if (componentSyncMsg.EntityId != shadowEntity.MailBox.Id)
        {
            throw new Exception(
                $"Invalid property full sync {componentSyncMsg.EntityId} {shadowEntity.MailBox.Id}");
        }

        Logger.Info($"On Component {componentSyncMsg.ComponentName} Sync Msg");
        shadowEntity.SyncComponent(componentSyncMsg.ComponentName, componentSyncMsg.PropertyTree);
        RpcClientHelper.ResolveComponetnSyncTask(componentSyncMsg.EntityId, componentSyncMsg.ComponentName);
    }
}