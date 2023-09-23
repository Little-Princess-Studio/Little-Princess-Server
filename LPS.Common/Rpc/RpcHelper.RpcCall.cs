// -----------------------------------------------------------------------
// <copyright file="RpcHelper.RpcCall.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc;

using System.Reflection;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Rpc.InnerMessages;

/// <summary>
/// Contains helper methods for making remote procedure calls (RPCs).
/// </summary>
public static partial class RpcHelper
{
    /// <summary>
    /// Call local entity's RPC method.
    /// </summary>
    /// <param name="entity">Entity object.</param>
    /// <param name="entityRpc">RPC message.</param>
    public static void CallLocalEntity(BaseEntity entity, EntityRpc entityRpc)
    {
        // todo: impl jit to compile methodInfo.invoke to expression.invoke to improve perf.
        var descriptor = GetRpcMethodArgTypes(entity.TypeId, entityRpc.MethodName);
        var authority = descriptor.Authority;
        var methodInfo = descriptor.Method;
        var rpcType = entityRpc.RpcType;

        DoAuthorityCheck(authority, rpcType, entityRpc);

        var args = ProtobufArgsToRpcArgList(entityRpc.Args, methodInfo);

        object? res;
        try
        {
            res = methodInfo.Invoke(entity, args);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to call rpc method.");
            return;
        }

        bool notifyOnly = entityRpc.NotifyOnly;
        var senderMailBox = entityRpc.SenderMailBox;
        var sendRpcType = GetCallBackType(entityRpc);

        if (res != null)
        {
            HandleRpcMethodResult(entity, entityRpc, methodInfo, res, senderMailBox, sendRpcType, notifyOnly);
        }
        else
        {
            if (!notifyOnly)
            {
                entity.SendRpcCallBackWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    sendRpcType,
                    null);
            }
        }
    }

    private static void DoAuthorityCheck(RpcStub.Authority authority, RpcType rpcType, EntityRpc entityRpc)
    {
        // Do authority check
        if (authority != RpcStub.Authority.All)
        {
            if (rpcType == RpcType.ServerInside)
            {
                if (!authority.HasFlag(RpcStub.Authority.ServerOnly))
                {
                    Logger.Warn($"Rpc method {entityRpc.MethodName} can only be called inside server.");
                    return;
                }
            }
            else if (rpcType == RpcType.ServerToClient)
            {
                if (!authority.HasFlag(RpcStub.Authority.ClientStub))
                {
                    Logger.Warn($"Rpc method {entityRpc.MethodName} can only be called from server to client.");
                    return;
                }
            }
            else if (rpcType == RpcType.ClientToServer)
            {
                if (!authority.HasFlag(RpcStub.Authority.ClientOnly))
                {
                    Logger.Warn($"Rpc method {entityRpc.MethodName} can only be called from client to server.");
                    return;
                }
            }
            else if (rpcType == RpcType.ServiceToEntity)
            {
                if (!authority.HasFlag(RpcStub.Authority.ServiceOnly))
                {
                    Logger.Warn($"Rpc method {entityRpc.MethodName} can only be called from service to entity.");
                    return;
                }
            }
        }
    }

    private static RpcType GetCallBackType(EntityRpc entityRpc)
    {
        var sendRpcType = RpcType.ServerInside;
        if (entityRpc.RpcType == RpcType.ClientToServer)
        {
            sendRpcType = RpcType.ServerToClient;
        }
        else if (entityRpc.RpcType == RpcType.ServerToClient)
        {
            sendRpcType = RpcType.ClientToServer;
        }
        else if (entityRpc.RpcType == RpcType.ServiceToEntity)
        {
            sendRpcType = RpcType.EntityToService;
        }

        return sendRpcType;
    }

    private static void HandleRpcMethodResult(
        BaseEntity entity,
        EntityRpc entityRpc,
        MethodInfo methodInfo,
        object res,
        InnerMessages.MailBox senderMailBox,
        RpcType sendRpcType,
        bool notifyOnly)
    {
        var returnType = methodInfo.ReturnType;
        if (returnType.IsGenericType)
        {
            // TODO: for performance, need using IL instead of dynamic/reflection?
            if (returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                SendTaskResult(entity, entityRpc, senderMailBox, sendRpcType, res, notifyOnly);
            }
            else if (returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                SendValueTaskResult(entity, entityRpc, senderMailBox, sendRpcType, res, notifyOnly);
            }
        }
        else if (returnType == typeof(Task))
        {
            ((Task)res).ContinueWith(task =>
            {
                if (task.Exception is not null)
                {
                    Logger.Error(task.Exception, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                entity.SendRpcCallBackWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    sendRpcType,
                    null);
            });
        }
        else if (returnType == typeof(ValueTask))
        {
            var task = (ValueTask)res;
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var e = task.AsTask().Exception!;
                    Logger.Error(e, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                entity.SendRpcCallBackWithRpcId(
                    entityRpc.RpcID,
                    PbMailBoxToRpcMailBox(senderMailBox),
                    sendRpcType,
                    null);
            }
            else
            {
                // if ValueTask not complete, alloc awaiter to wait
                task.AsTask().ContinueWith((t) =>
                {
                    if (t.Exception is not null)
                    {
                        Logger.Error(t.Exception, "Failed to call rpc method.");
                        return;
                    }

                    if (notifyOnly)
                    {
                        return;
                    }

                    entity.SendRpcCallBackWithRpcId(
                        entityRpc.RpcID,
                        PbMailBoxToRpcMailBox(senderMailBox),
                        sendRpcType,
                        null);
                });
            }
        }
    }

    private static void SendValueTaskResult(
        BaseEntity entity,
        EntityRpc entityRpc,
        InnerMessages.MailBox senderMailBox,
        RpcType sendRpcType,
        in object res,
        bool notifyOnly)
    {
        void SendDynamic(dynamic t) =>
            entity.SendRpcCallBackWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);

        void Send<T>(in ValueTask<T> t) => entity.SendRpcCallBackWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);

        void HandleValueTask<T>(in ValueTask<T> task)
        {
            // ValueTask should always be sync
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var e = task.AsTask().Exception!;
                    Logger.Error(e, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                Send(task);
            }
            else
            {
                // if ValueTask not complete, alloc awaiter to wait
                task.AsTask().ContinueWith((t) =>
                {
                    if (t.Exception is not null)
                    {
                        Logger.Error(t.Exception, "Failed to call rpc method.");
                        return;
                    }

                    if (notifyOnly)
                    {
                        return;
                    }

                    entity.SendRpcCallBackWithRpcId(
                        entityRpc.RpcID,
                        PbMailBoxToRpcMailBox(senderMailBox),
                        sendRpcType,
                        t.Result);
                });
            }
        }

        void HandleValueTaskDynamic(dynamic task)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var e = task.AsTask().Exception!;
                    Logger.Error(e, "Failed to call rpc method.");
                    return;
                }

                if (notifyOnly)
                {
                    return;
                }

                SendDynamic(task);
            }
            else
            {
                task.AsTask().ContinueWith(new Action(() =>
                {
                    if (task.Exception is not null)
                    {
                        Logger.Error(task.Exception, "Failed to call rpc method.");
                        return;
                    }

                    if (notifyOnly)
                    {
                        return;
                    }

                    entity.SendRpcCallBackWithRpcId(
                        entityRpc.RpcID,
                        PbMailBoxToRpcMailBox(senderMailBox),
                        sendRpcType,
                        task.Result);
                }));
            }
        }

        switch (res)
        {
            case ValueTask<int> task:
                HandleValueTask(task);
                break;
            case ValueTask<string> task:
                HandleValueTask(task);
                break;
            case ValueTask<float> task:
                HandleValueTask(task);
                break;
            case ValueTask<LPS.Common.Rpc.MailBox> task:
                HandleValueTask(task);
                break;
            case ValueTask<bool> task:
                HandleValueTask(task);
                break;
            default:
                {
                    dynamic task = res;
                    HandleValueTaskDynamic(task);
                }

                break;
        }
    }

    private static void SendTaskResult(
        BaseEntity entity,
        EntityRpc entityRpc,
        LPS.Common.Rpc.InnerMessages.MailBox senderMailBox,
        RpcType sendRpcType,
        in object res,
        bool notifyOnly)
    {
        void SendDynamic(dynamic t)
        {
            if (t.Exception is not null)
            {
                Logger.Error(t.Exception, "Failed to call rpc method.");
                return;
            }

            if (notifyOnly)
            {
                return;
            }

            entity.SendRpcCallBackWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);
        }

        void Send<T>(Task<T> t)
        {
            if (t.Exception is not null)
            {
                Logger.Error(t.Exception, "Failed to call rpc method.");
                return;
            }

            if (notifyOnly)
            {
                return;
            }

            entity.SendRpcCallBackWithRpcId(
                entityRpc.RpcID,
                PbMailBoxToRpcMailBox(senderMailBox),
                sendRpcType,
                t.Result);
        }

        switch (res)
        {
            case Task<int> task:
                task.ContinueWith(Send);
                break;
            case Task<string> task:
                task.ContinueWith(Send);
                break;
            case Task<float> task:
                task.ContinueWith(Send);
                break;
            case Task<MailBox> task:
                task.ContinueWith(Send);
                break;
            case Task<bool> task:
                task.ContinueWith(Send);
                break;
            default:
                {
                    dynamic task = res;
                    task.ContinueWith((Action<dynamic>)SendDynamic);
                }

                break;
        }
    }
}