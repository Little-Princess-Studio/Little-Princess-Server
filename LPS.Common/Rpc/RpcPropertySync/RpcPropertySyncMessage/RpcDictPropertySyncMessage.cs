// -----------------------------------------------------------------------
// <copyright file="RpcDictPropertySyncMessage.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages.ProtobufDefs;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// RPC dict property sync message.
/// </summary>
public class RpcDictPropertySyncMessage : RpcPropertySyncMessage
{
    private readonly IRpcDictPropertySyncMessageImpl? impl;

    /// <summary>
    /// Dict sync operation.
    /// </summary>
    /// <param name="args">Operation arguments.</param>
    public delegate void DictOperation(params object[] args);

    /// <summary>
    /// Gets the sync message operation.
    /// </summary>
    public DictOperation? Action { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcDictPropertySyncMessage"/> class.
    /// </summary>
    /// <param name="mailbox">MailBox of the syncing entity.</param>
    /// <param name="operation">Sync operation.</param>
    /// <param name="rpcPropertyPath">Sync property path in property tree.</param>
    public RpcDictPropertySyncMessage(
        MailBox mailbox,
        RpcPropertySyncOperation operation,
        string rpcPropertyPath)
        : base(mailbox, operation, rpcPropertyPath, RpcSyncPropertyType.Dict)
    {
        switch (operation)
        {
            case RpcPropertySyncOperation.UpdatePair:
                this.impl = new RpcDictPropertyUpdatePairSyncMessageImpl();
                break;
            case RpcPropertySyncOperation.RemoveElem:
                this.impl = new RpcDictPropertyRemoveSyncMessageImpl();
                break;
            case RpcPropertySyncOperation.Clear:
                break;
            case RpcPropertySyncOperation.SetValue:
                this.impl = new RpcDictPropertySetValueSyncMessageImpl();
                break;
            case RpcPropertySyncOperation.AddListElem:
            case RpcPropertySyncOperation.InsertElem:
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }

        this.Action = operation switch
        {
            RpcPropertySyncOperation.UpdatePair => args =>
            {
                var key = args[0] as string;

                if (args[1] is not RpcPropertyContainer val)
                {
                    throw new Exception($"Invalid args {args}");
                }

                ((RpcDictPropertyUpdatePairSyncMessageImpl)this.impl!).Update(key!, val);
            },
            RpcPropertySyncOperation.RemoveElem => args =>
            {
                var key = args[0] as string;

                ((RpcDictPropertyRemoveSyncMessageImpl)this.impl!).Remove(key!);
            },
            RpcPropertySyncOperation.SetValue => args =>
            {
                var val = args[0] as RpcPropertyContainer;
                ((RpcDictPropertySetValueSyncMessageImpl)this.impl!).SetValue(val!);
            },
            RpcPropertySyncOperation.Clear => args => { },
            _ => throw new Exception($"Invalid operation {operation}"),
        };
    }

    /// <inheritdoc/>
    public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg) =>
        this.impl!.MergeKeepOrder((otherMsg as RpcDictPropertySyncMessage)!);

    /// <inheritdoc/>
    public override void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo)
    {
        var dictInfo = (rpcPropertySyncInfo as RpcDictPropertySyncInfo)!;

        // if we got a clear msg,
        // then cancel all the previous modification and record the clear msg
        if (this.Operation == RpcPropertySyncOperation.Clear || this.Operation == RpcPropertySyncOperation.SetValue)
        {
            dictInfo.PropPath2SyncMsgQueue.Clear();
            dictInfo.Enque(this);
            return;
        }

        if (dictInfo.PropPath2SyncMsgQueue.Count == 0)
        {
            dictInfo.Enque(this);
        }
        else
        {
            var res = this.impl!.MergeIntoSyncInfo(dictInfo);
            if (!res)
            {
                dictInfo.Enque(this);
            }
        }
    }

    /// <inheritdoc/>
    public override PropertySyncCommand Serialize()
    {
        if (this.Operation == RpcPropertySyncOperation.Clear)
        {
            return new PropertySyncCommand()
            {
                Operation = SyncOperation.Clear,
            };
        }

        return this.impl!.ToSyncCommand();
    }

    /// <summary>
    /// Gets the impl object of the message.
    /// </summary>
    /// <typeparam name="T">Type of the impl message.</typeparam>
    /// <returns>The implementation object of the message.</returns>
    public T GetImpl<T>()
        where T : IRpcDictPropertySyncMessageImpl => (T)this.impl!;

#pragma warning disable SA1600
    public interface IRpcDictPropertySyncMessageImpl : RpcListPropertySyncMessage.IRpcPropertySyncMessageImpl
    {
        bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo);

        bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg);
    }

    public class RpcDictPropertySetValueSyncMessageImpl : IRpcDictPropertySyncMessageImpl
    {
        private RpcPropertyContainer? value;

        public void SetValue(RpcPropertyContainer value) => this.value = value;

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.SetValue,
            };

            cmd.Args.Add(this.value!.ToRpcArg());

            return cmd;
        }

        public bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo)
        {
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.SetValue)
            {
                var setValueImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertySetValueSyncMessageImpl>();
                setValueImpl.value = this.value;
                return true;
            }

            return true;
        }

        public bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            var setValueImpl = newMsg.GetImpl<RpcDictPropertySetValueSyncMessageImpl>();
            this.value = setValueImpl.value;

            return true;
        }
    }

    public class RpcDictPropertyUpdatePairSyncMessageImpl : IRpcDictPropertySyncMessageImpl
    {
        private readonly Dictionary<string, RpcPropertyContainer> updateDictInfo = new();

        public Dictionary<string, RpcPropertyContainer> GetUpdateDictInfo() => this.updateDictInfo;

        public void Update(string key, RpcPropertyContainer value)
        {
            this.updateDictInfo[key] = value;
        }

        public bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo)
        {
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.UpdatePair)
            {
                var updateImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyUpdatePairSyncMessageImpl>();
                foreach (var (key, value) in this.updateDictInfo)
                {
                    updateImpl.updateDictInfo[key] = value;
                }

                return true;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.RemoveElem)
            {
                var removeImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
                var removedKeys = removeImpl.GetRemoveDictInfo();
                foreach (var (key, value) in this.updateDictInfo)
                {
                    if (removedKeys.Contains(key))
                    {
                        removedKeys.Remove(key);
                    }
                }

                if (removedKeys.Count == 0)
                {
                    rpcDictPropertySyncInfo.PopLastMsg();
                    return false;
                }
            }

            return true;
        }

        public bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.UpdatePair)
            {
                return false;
            }

            var dictUpdateImpl = newMsg.GetImpl<RpcDictPropertyUpdatePairSyncMessageImpl>();
            var dictUpdateInfo = dictUpdateImpl.GetUpdateDictInfo();
            foreach (var (key, value) in dictUpdateInfo)
            {
                this.updateDictInfo[key] = value;
            }

            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.UpdatePair,
            };

            var dictWithStringKeyArg = new DictWithStringKeyArg();
            foreach (var (key, value) in this.updateDictInfo)
            {
                dictWithStringKeyArg.PayLoad.Add(key, value.ToRpcArg());
            }

            cmd.Args.Add(Any.Pack(dictWithStringKeyArg));
            return cmd;
        }
    }

    public class RpcDictPropertyRemoveSyncMessageImpl : IRpcDictPropertySyncMessageImpl
    {
        private readonly HashSet<string> removeDictInfo = new();

        public HashSet<string> GetRemoveDictInfo() => this.removeDictInfo;

        public void Remove(string key)
        {
            this.removeDictInfo.Add(key);
        }

        public bool MergeIntoSyncInfo(RpcDictPropertySyncInfo rpcDictPropertySyncInfo)
        {
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.RemoveElem)
            {
                var removeImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
                foreach (var key in this.removeDictInfo)
                {
                    removeImpl.removeDictInfo.Add(key);
                }

                return true;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.UpdatePair)
            {
                var updateImpl =
                    (lastMsg as RpcDictPropertySyncMessage)!.GetImpl<RpcDictPropertyUpdatePairSyncMessageImpl>();
                var updateInfo = updateImpl.GetUpdateDictInfo();
                foreach (var key in this.removeDictInfo)
                {
                    if (updateInfo.ContainsKey(key))
                    {
                        updateInfo.Remove(key);
                    }
                }

                if (updateInfo.Count == 0)
                {
                    rpcDictPropertySyncInfo.PopLastMsg();

                    // if return false, this msg will be enqueued
                    return false;
                }
            }

            return true;
        }

        public bool MergeKeepOrder(RpcDictPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.RemoveElem)
            {
                return false;
            }

            var dictRemoveImpl = newMsg.GetImpl<RpcDictPropertyRemoveSyncMessageImpl>();
            var dictRemoveInfo = dictRemoveImpl.GetRemoveDictInfo();
            foreach (var key in dictRemoveInfo)
            {
                this.removeDictInfo.Add(key);
            }

            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand()
            {
                Operation = SyncOperation.RemoveElem,
            };

            foreach (var key in this.removeDictInfo)
            {
                cmd.Args.Add(Any.Pack(new StringArg()
                {
                    PayLoad = key,
                }));
            }

            return cmd;
        }
    }
#pragma warning restore SA1600
}