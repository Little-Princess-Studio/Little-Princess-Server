// -----------------------------------------------------------------------
// <copyright file="TimeCircleSlot.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Ipc;

using LPS.Common.Core.Debug;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcPropertySync;
using MailBox = LPS.Common.Core.Rpc.MailBox;

/// <summary>
/// Slot of the time circle.
/// </summary>
public class TimeCircleSlot
{
    // public const int MaxSlotMessageSize = 2048;
    // private int slotMassageCount_;
    // entityID => { propertyPath => RpcPropertySyncMessage }
    // public bool Full() => slotMassageCount_ >= MaxSlotMessageSize;
#pragma warning disable SA1305
    private readonly Dictionary<string, Dictionary<string, RpcPropertySyncInfo>> idToSyncMsg = new();
    private readonly Dictionary<string, Queue<RpcPropertySyncMessage>> idToSyncMsgWithOrder = new();
#pragma warning restore SA1305
    /// <summary>
    /// Clear the slot.
    /// </summary>
    public void Clear()
    {
        // slotMassageCount_ = 0;
        this.idToSyncMsg.Clear();
        this.idToSyncMsgWithOrder.Clear();
    }

    // private void IncreaseMessageCount() {
    //     lock(this)
    //     {
    //         ++slotMassageCount_;
    //     }
    // }

    /// <summary>
    /// Find the ordered sync queue from mailbox.
    /// </summary>
    /// <param name="mb">Mailbox.</param>
    /// <returns>Message queue of the mailbox.</returns>
    public Queue<RpcPropertySyncMessage>? FindOrderedSyncQueue(MailBox mb) =>
        this.idToSyncMsgWithOrder.ContainsKey(mb.Id) ? this.idToSyncMsgWithOrder[mb.Id] : null;

    /// <summary>
    /// Find the none-ordered sync queue from mailbox.
    /// </summary>
    /// <param name="mb">Mailbox.</param>
    /// <returns>Message queue of the mailbox.</returns>
    public int GetSyncQueueLength(MailBox mb) =>
        this.idToSyncMsgWithOrder.ContainsKey(mb.Id) ? this.idToSyncMsgWithOrder[mb.Id].Count : 0;

    /// <summary>
    /// Find the rpc property sync info.
    /// </summary>
    /// <param name="mb">Mailbox.</param>
    /// <param name="path">Path of the property.</param>
    /// <returns>Rpc property sync info.</returns>
    public RpcPropertySyncInfo? FindRpcPropertySyncInfo(MailBox mb, string path)
    {
        var id = mb.Id;
        if (!this.idToSyncMsg.ContainsKey(id))
        {
            return null;
        }

        var info = this.idToSyncMsg[id];

        if (!info.ContainsKey(path))
        {
            return null;
        }

        return info[path];
    }

    /// <summary>
    /// Add keep order sync message to queue.
    /// </summary>
    /// <param name="incomeMsg">Income sync message.</param>
    public void AddSyncMessageKeepOrder(RpcPropertySyncMessage incomeMsg)
    {
        var id = incomeMsg.MailBox.Id;

        lock (this)
        {
            Queue<RpcPropertySyncMessage> queue;

            if (this.idToSyncMsgWithOrder.ContainsKey(id))
            {
                queue = this.idToSyncMsgWithOrder[id];
            }
            else
            {
                queue = new Queue<RpcPropertySyncMessage>();
                this.idToSyncMsgWithOrder[id] = queue;
            }

            if (queue.Count > 0)
            {
                var lastMsg = queue.Last();
                var res = lastMsg.MergeKeepOrder(incomeMsg);
                if (res)
                {
                    return;
                }
            }

            queue.Enqueue(incomeMsg);
        }
    }

    /// <summary>
    /// Add no-keep order sync message to queue.
    /// </summary>
    /// <param name="incomeMsg">Income sync message.</param>
    /// <exception cref="ArgumentOutOfRangeException">ArgumentOutOfRangeException.</exception>
    public void AddSyncMessageNoKeepOrder(RpcPropertySyncMessage incomeMsg)
    {
        lock (this)
        {
            Func<RpcPropertySyncInfo> getSyncInfoFunc = incomeMsg.RpcSyncPropertyType switch
            {
                RpcSyncPropertyType.PlaintAndCostume => () => new RpcPlaintAndCostumePropertySyncInfo(),
                RpcSyncPropertyType.List => () => new RpcListPropertySyncInfo(),
                RpcSyncPropertyType.Dict => () => new RpcDictPropertySyncInfo(),
                _ => throw new ArgumentOutOfRangeException(),
            };

            var syncInfo = this.GetRpcPropertySyncInfo(
                incomeMsg.MailBox,
                incomeMsg.RpcPropertyPath,
                getSyncInfoFunc);

            syncInfo.AddNewSyncMessage(incomeMsg);
        }
    }

    /// <summary>
    /// Dispatch property sync command list.
    /// </summary>
    /// <param name="dispatch">Dispatch handler.</param>
    public void Dispatch(Action<PropertySyncCommandList> dispatch)
    {
        foreach (var (entityId, entitySyncInfoDict) in this.idToSyncMsg)
        {
            Logger.Debug($"Dispatch entity id {entityId}, no ordered");
            foreach (var (propPath, syncInfo) in entitySyncInfoDict)
            {
                PropertySyncCommandList cmdList = new()
                {
                    Path = propPath,
                    EntityId = entityId,
                    PropType = (SyncPropType)syncInfo.RpcSyncPropertyType,
                };

                foreach (var msg in syncInfo.PropPath2SyncMsgQueue)
                {
                    Logger.Debug($"{propPath} -> {msg}");
                    var syncMsg = msg.Serialize();
                    cmdList.SyncArg.Add(syncMsg);
                }

                dispatch.Invoke(cmdList);
            }
        }

        foreach (var (entityId, msgQueue) in this.idToSyncMsgWithOrder)
        {
            Logger.Debug($"Dispatch entity id {entityId}, ordered");
            while (msgQueue.Count > 0)
            {
                var msg = msgQueue.Dequeue();
                var propPath = msg.RpcPropertyPath;
                Logger.Debug($"{propPath} -> {msg}");
                PropertySyncCommandList cmdList = new()
                {
                    Path = propPath,
                    EntityId = entityId,
                    PropType = (SyncPropType)msg.RpcSyncPropertyType,
                };

                cmdList.SyncArg.Add(msg.Serialize());
                dispatch.Invoke(cmdList);
            }
        }
    }

    private RpcPropertySyncInfo GetRpcPropertySyncInfo(
        MailBox mb, string path, Func<RpcPropertySyncInfo> getSyncInfoFunc)
    {
        var id = mb.Id;
        if (this.idToSyncMsg.ContainsKey(id))
        {
            var entitySyncInfoDict = this.idToSyncMsg[id];
            if (entitySyncInfoDict.ContainsKey(path))
            {
                var syncInfo = entitySyncInfoDict[path];
                return syncInfo;
            }

            var newSyncInfo = getSyncInfoFunc();
            entitySyncInfoDict[path] = newSyncInfo;
            return newSyncInfo;
        }
        else
        {
            var newEntitySyncInfoDict = new Dictionary<string, RpcPropertySyncInfo>();
            var newSyncInfo = getSyncInfoFunc();
            this.idToSyncMsg[id] = newEntitySyncInfoDict;
            newEntitySyncInfoDict[path] = newSyncInfo;
            return newSyncInfo;
        }
    }
}