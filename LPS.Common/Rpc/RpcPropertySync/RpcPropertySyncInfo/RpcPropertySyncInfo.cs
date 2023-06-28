// -----------------------------------------------------------------------
// <copyright file="RpcPropertySyncInfo.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;

using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

/// <summary>
/// RPC sync property type.
/// </summary>
public enum RpcSyncPropertyType
{
    /// <summary>
    /// Plaint or Costume type.
    /// </summary>
    PlaintAndCostume = 0,

    /// <summary>
    /// RPC dict type.
    /// </summary>
    Dict = 1,

    /// <summary>
    /// RPC list type.
    /// </summary>
    List = 2,
}

/// <summary>
/// Base class of RPC property sync information.
/// </summary>
public abstract class RpcPropertySyncInfo
{
    private readonly LinkedList<RpcPropertySyncMessage> propPath2SyncMsgQueue = new();

    /// <summary>
    /// Gets the sync message queue.
    /// </summary>
    public LinkedList<RpcPropertySyncMessage> PropPath2SyncMsgQueue => this.propPath2SyncMsgQueue;

    /// <summary>
    /// Add new sync message to sync message queue.
    /// </summary>
    /// <param name="msg">Sync message.</param>
    public abstract void AddNewSyncMessage(RpcPropertySyncMessage msg);

    /// <summary>
    /// Rpc sync property type.
    /// </summary>
    public readonly RpcSyncPropertyType RpcSyncPropertyType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPropertySyncInfo"/> class.
    /// </summary>
    /// <param name="rpcSyncPropertyType">Rpc sync property type.</param>
    protected RpcPropertySyncInfo(RpcSyncPropertyType rpcSyncPropertyType) =>
        this.RpcSyncPropertyType = rpcSyncPropertyType;

    /// <summary>
    /// Add new sync message to sync message queue.
    /// </summary>
    /// <param name="msg">New sync message.</param>
    public void Enque(RpcPropertySyncMessage msg) => this.propPath2SyncMsgQueue.AddLast(msg);

    /// <summary>
    /// Get the last sync message in the sync queue.
    /// </summary>
    /// <returns>The last sync message.</returns>
    public RpcPropertySyncMessage? GetLastMsg()
        => this.propPath2SyncMsgQueue.Count > 0 ? this.propPath2SyncMsgQueue.Last() : null;

    /// <summary>
    /// Pop the last message from the sync message queue.
    /// </summary>
    public void PopLastMsg()
    {
        if (this.propPath2SyncMsgQueue.Count > 0)
        {
            this.propPath2SyncMsgQueue.RemoveLast();
        }
    }

    /// <summary>
    /// Reset sync queue.
    /// </summary>
    public void Clear() => this.propPath2SyncMsgQueue.Clear();
}