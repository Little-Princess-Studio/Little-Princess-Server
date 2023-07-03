// -----------------------------------------------------------------------
// <copyright file="RpcListPropertySyncMessage.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;

/// <summary>
/// operation on List is very complex, so RpcListPropertySyncInfo
/// will only merge same operation for a continuous operation sequence.
/// </summary>
public class RpcListPropertySyncMessage : RpcPropertySyncMessage
{
    private readonly IRpcListPropertySyncMessageImpl? impl;

    /// <summary>
    /// List sync operation.
    /// </summary>
    /// <param name="args">Operation arguments.</param>
    public delegate void ListOperation(params object[] args);

    /// <summary>
    /// Gets the sync message operation.
    /// </summary>
    public ListOperation? Action { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcListPropertySyncMessage"/> class.
    /// </summary>
    /// <param name="mailbox">MailBox of the syncing entity.</param>
    /// <param name="operation">Sync operation.</param>
    /// <param name="rpcPropertyPath">Sync property path in property tree.</param>
    public RpcListPropertySyncMessage(
        Rpc.MailBox mailbox,
        RpcPropertySyncOperation operation,
        string rpcPropertyPath)
        : base(mailbox, operation, rpcPropertyPath, RpcSyncPropertyType.List)
    {
        switch (operation)
        {
            case RpcPropertySyncOperation.AddListElem:
                this.impl = new RpcListPropertyAddElemSyncMessageImpl();
                this.Action = args =>
                {
                    var elem = args[0] as RpcPropertyContainer;
                    if (elem == null)
                    {
                        throw new Exception($"Invalid args {args}");
                    }

                    ((RpcListPropertyAddElemSyncMessageImpl)this.impl).AddElem(elem);
                };
                break;
            case RpcPropertySyncOperation.RemoveElem:
                this.impl = new RpcListPropertyRemoveElemMessageImpl();
                this.Action = args =>
                {
                    var index = (int)args[0];
                    ((RpcListPropertyRemoveElemMessageImpl)this.impl).RemoveElem(index);
                };
                break;
            case RpcPropertySyncOperation.Clear:
                this.impl = null;
                this.Action = null;
                break;
            case RpcPropertySyncOperation.InsertElem:
                this.impl = new RpcListPropertyInsertSyncMessageImpl();
                this.Action = args =>
                {
                    var index = (int)args[0];
                    var elem = args[1] as RpcPropertyContainer;
                    if (elem == null)
                    {
                        throw new Exception($"Invalid args {args}");
                    }

                    ((RpcListPropertyInsertSyncMessageImpl)this.impl).Insert(index, elem);
                };
                break;
            case RpcPropertySyncOperation.UpdatePair:
                this.impl = new RpcListPropertyUpdatePairSyncMessageImpl();
                this.Action = args =>
                {
                    var index = (int)args[0];
                    var elem = args[1] as RpcPropertyContainer;
                    if (elem == null)
                    {
                        throw new Exception($"Invalid args {args}");
                    }

                    ((RpcListPropertyUpdatePairSyncMessageImpl)this.impl).SetValue(index, elem);
                };
                break;
            case RpcPropertySyncOperation.SetValue:
                this.impl = new RpcListPropertySetValueSyncMessageImpl();
                this.Action = args =>
                {
                    var val = args[0] as RpcPropertyContainer;
                    ((RpcListPropertySetValueSyncMessageImpl)this.impl!).SetValue(val!);
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    /// <inheritdoc/>
    public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
        => this.impl!.MergeKeepOrder((otherMsg as RpcListPropertySyncMessage)!);

    /// <inheritdoc/>
    public override void MergeIntoSyncInfo(
        RpcPropertySync.RpcPropertySyncInfo.RpcPropertySyncInfo rpcPropertySyncInfo)
    {
        var rpcListPropertySyncInfo = (rpcPropertySyncInfo as RpcListPropertySyncInfo)!;
        if (this.Operation is RpcPropertySyncOperation.Clear or RpcPropertySyncOperation.SetValue)
        {
            rpcListPropertySyncInfo.Clear();
            rpcListPropertySyncInfo.Enque(this);
        }
        else
        {
            var mergeRes = this.impl!.MergeIntoSyncInfo(rpcListPropertySyncInfo);
            if (!mergeRes)
            {
                rpcListPropertySyncInfo.Enque(this);
            }
        }
    }

    /// <summary>
    /// Gets the impl object of the message.
    /// </summary>
    /// <typeparam name="T">Type of the impl message.</typeparam>
    /// <returns>The implementation object of the message.</returns>
    public T GetImpl<T>()
        where T : IRpcPropertySyncMessageImpl => (T)this.impl!;

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

#pragma warning disable SA1600
    public interface IRpcPropertySyncMessageImpl
    {
        PropertySyncCommand ToSyncCommand();
    }

    public interface IRpcListPropertySyncMessageImpl : IRpcPropertySyncMessageImpl
    {
        bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo);

        bool MergeKeepOrder(RpcListPropertySyncMessage newMsg);
    }

    public class RpcListPropertySetValueSyncMessageImpl : IRpcListPropertySyncMessageImpl
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

        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcDictPropertySyncInfo)
        {
            var lastMsg = rpcDictPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation == RpcPropertySyncOperation.SetValue)
            {
                var setValueImpl =
                    (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertySetValueSyncMessageImpl>();
                setValueImpl.value = this.value;
                return true;
            }

            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            var setValueImpl = newMsg.GetImpl<RpcListPropertySetValueSyncMessageImpl>();
            this.value = setValueImpl.value;

            return true;
        }
    }

    public class RpcListPropertyAddElemSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<RpcPropertyContainer> addInfo = new();

        public List<RpcPropertyContainer> GetAddInfo() => this.addInfo;

        public void AddElem(RpcPropertyContainer elem) => this.addInfo.Add(elem);

        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.AddListElem)
            {
                return false;
            }

            var addImpl = (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertyAddElemSyncMessageImpl>();
            var newAddImpl = this;
            addImpl.addInfo.AddRange(newAddImpl.addInfo);
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.AddListElem)
            {
                return false;
            }

            var newAddImpl = newMsg.GetImpl<RpcListPropertyAddElemSyncMessageImpl>();
            this.addInfo.AddRange(newAddImpl.addInfo);
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.AddListElem,
            };

            foreach (var rpcPropertyContainer in this.addInfo)
            {
                cmd.Args.Add(rpcPropertyContainer.ToRpcArg());
            }

            return cmd;
        }
    }

    public class RpcListPropertyUpdatePairSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly Dictionary<int, RpcPropertyContainer> setValueInfo = new();

        public Dictionary<int, RpcPropertyContainer> GetSetValueInfo() => this.setValueInfo;

        public void SetValue(int index, RpcPropertyContainer elem) => this.setValueInfo[index] = elem;

        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            var setValueImpl = (lastMsg as RpcListPropertySyncMessage)!
                .GetImpl<RpcListPropertyUpdatePairSyncMessageImpl>();
            var newSetValueImpl = this;
            foreach (var kv in newSetValueImpl.setValueInfo)
            {
                setValueImpl.setValueInfo[kv.Key] = kv.Value;
            }

            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.SetValue)
            {
                return false;
            }

            var newSetValueImpl = newMsg.GetImpl<RpcListPropertyUpdatePairSyncMessageImpl>();
            foreach (var kv in newSetValueImpl.setValueInfo)
            {
                this.setValueInfo[kv.Key] = kv.Value;
            }

            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.SetValue,
            };

            var dictWithIntKeyArg = new DictWithIntKeyArg();
            foreach (var (key, value) in this.setValueInfo)
            {
                dictWithIntKeyArg.PayLoad.Add(key, value.ToRpcArg());
            }

            cmd.Args.Add(Any.Pack(dictWithIntKeyArg));
            return cmd;
        }
    }

    public class RpcListPropertyInsertSyncMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<(int, RpcPropertyContainer)> insertInfo = new();

        public List<(int Index, RpcPropertyContainer Value)> GetInsertInfo() => this.insertInfo;

        public void Insert(int index, RpcPropertyContainer elem)
        {
            this.insertInfo.Add((index, elem));
        }

        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.InsertElem)
            {
                return false;
            }

            var insertImpl =
                (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertyInsertSyncMessageImpl>();
            var newInsertImpl = this;
            insertImpl.insertInfo.AddRange(newInsertImpl.insertInfo);
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.InsertElem)
            {
                return false;
            }

            var newRemoveImpl = newMsg.GetImpl<RpcListPropertyInsertSyncMessageImpl>();
            this.insertInfo.AddRange(newRemoveImpl.insertInfo);
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.InsertElem,
            };

            foreach (var (index, rpcPropertyContainer) in this.insertInfo)
            {
                cmd.Args.Add(Any.Pack(new PairWithIntKey
                {
                    Key = index,
                    Value = rpcPropertyContainer.ToRpcArg(),
                }));
            }

            return cmd;
        }
    }

    public class RpcListPropertyRemoveElemMessageImpl : IRpcListPropertySyncMessageImpl
    {
        private readonly List<int> removeElemInfo = new();

        public List<int> GetRemoveElemInfo() => this.removeElemInfo;

        public void RemoveElem(int index)
        {
            this.removeElemInfo.Add(index);
        }

        public bool MergeIntoSyncInfo(RpcListPropertySyncInfo rpcListPropertySyncInfo)
        {
            var lastMsg = rpcListPropertySyncInfo.GetLastMsg();
            if (lastMsg == null)
            {
                return false;
            }

            if (lastMsg.Operation != RpcPropertySyncOperation.RemoveElem)
            {
                return false;
            }

            var removeImpl =
                (lastMsg as RpcListPropertySyncMessage)!.GetImpl<RpcListPropertyRemoveElemMessageImpl>();
            var newRemoveImpl = this;
            removeImpl.removeElemInfo.AddRange(newRemoveImpl.removeElemInfo);
            return true;
        }

        public bool MergeKeepOrder(RpcListPropertySyncMessage newMsg)
        {
            if (newMsg.Operation != RpcPropertySyncOperation.RemoveElem)
            {
                return false;
            }

            var newRemoveImpl = newMsg.GetImpl<RpcListPropertyRemoveElemMessageImpl>();
            this.removeElemInfo.AddRange(newRemoveImpl.removeElemInfo);
            return true;
        }

        public PropertySyncCommand ToSyncCommand()
        {
            var cmd = new PropertySyncCommand
            {
                Operation = SyncOperation.RemoveElem,
            };

            foreach (var index in this.removeElemInfo)
            {
                cmd.Args.Add(Any.Pack(new IntArg
                {
                    PayLoad = index,
                }));
            }

            return cmd;
        }
    }
#pragma warning restore SA1600
}