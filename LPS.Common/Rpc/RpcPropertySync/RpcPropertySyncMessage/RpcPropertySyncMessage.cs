// -----------------------------------------------------------------------
// <copyright file="RpcPropertySyncMessage.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// RPC Property sync operation.
/// </summary>
public enum RpcPropertySyncOperation
{
    /// <summary>
    /// Set value.
    /// </summary>
    SetValue = 0,

    /// <summary>
    /// Update key-value pair for RPC dict or index-value for RPC list.
    /// </summary>
    UpdatePair = 1,

    /// <summary>
    /// Add elem to RPC list.
    /// </summary>
    AddListElem = 2,

    /// <summary>
    /// Remove elem from RPC list or RPC dict.
    /// </summary>
    RemoveElem = 3,

    /// <summary>
    /// Clear RPC list or RPC dict.
    /// </summary>
    Clear = 4,

    /// <summary>
    /// Insert elem to RPC list.
    /// </summary>
    InsertElem = 5,
}

/// <summary>
/// Base class for RPC property sync message.
/// </summary>
public abstract class RpcPropertySyncMessage
{
    /// <summary>
    /// MailBox of the sync entity.
    /// </summary>
    public readonly MailBox MailBox;

    /// <summary>
    /// Sync operation.
    /// </summary>
    public readonly RpcPropertySyncOperation Operation;

    /// <summary>
    /// Sync path in property tree.
    /// </summary>
    public readonly string RpcPropertyPath;

    /// <summary>
    /// Indicates whether this is a components sync message.
    /// </summary>
    public readonly bool IsComponentsSyncMsg;

    /// <summary>
    /// Name of the component associated with the sync message.
    /// </summary>
    public readonly string ComponentName;

    /// <summary>
    /// RPC property sync type.
    /// </summary>
    public readonly RpcSyncPropertyType RpcSyncPropertyType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPropertySyncMessage"/> class.
    /// </summary>
    /// <param name="mailbox">MailBox of the sync entity.</param>
    /// <param name="operation">Sync operation.</param>
    /// <param name="rpcPropertyPath">Sync path in property tree.</param>
    /// <param name="rpcSyncPropertyType">RPC property sync type.</param>
    /// <param name="isComponentsSyncMsg">Indicates whether this is a components sync message.</param>
    /// <param name="componentName">Name of the component associated with the sync message.</param>
    public RpcPropertySyncMessage(
        MailBox mailbox,
        RpcPropertySyncOperation operation,
        string rpcPropertyPath,
        RpcSyncPropertyType rpcSyncPropertyType,
        bool isComponentsSyncMsg,
        string componentName)
    {
        this.MailBox = mailbox;
        this.Operation = operation;
        this.RpcPropertyPath = rpcPropertyPath;
        this.RpcSyncPropertyType = rpcSyncPropertyType;
        this.IsComponentsSyncMsg = isComponentsSyncMsg;
        this.ComponentName = componentName;
    }

    /// <summary>
    /// Merge with a sync message where the message should keep order when syncing.
    /// </summary>
    /// <param name="otherMsg">Message to merge with.</param>
    /// <returns>If success to merge.</returns>
    public abstract bool MergeKeepOrder(RpcPropertySyncMessage otherMsg);

    /// <summary>
    /// Merge this sync message to <see cref="RpcPropertySyncInfo"/>, used for no-keep-order syncing.
    /// </summary>
    /// <param name="rpcPropertySyncInfo">RpcPropertySyncInfo to merge.</param>
    public abstract void MergeIntoSyncInfo(RpcPropertySync.RpcPropertySyncInfo.RpcPropertySyncInfo rpcPropertySyncInfo);

    /// <summary>
    /// Serialize sync message to protobuf sync message.
    /// </summary>
    /// <returns>Protobuf sync message.</returns>
    public abstract PropertySyncCommand Serialize();
}