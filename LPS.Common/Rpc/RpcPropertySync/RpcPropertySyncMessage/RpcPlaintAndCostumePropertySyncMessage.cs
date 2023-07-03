// -----------------------------------------------------------------------
// <copyright file="RpcPlaintAndCostumePropertySyncMessage.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Sync message for RPC pliant or costume property.
/// </summary>
public class RpcPlaintAndCostumePropertySyncMessage : RpcPropertySyncMessage
{
    /// <summary>
    /// Gets or sets the value of the sync mesasge.
    /// </summary>
    public RpcPropertyContainer Val { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPlaintAndCostumePropertySyncMessage"/> class.
    /// </summary>
    /// <param name="mailbox">MailBox of the entity starting the sync.</param>
    /// <param name="operation">Sync operation.</param>
    /// <param name="rpcPropertyPath">Property path in the property tree.</param>
    /// <param name="val">Value to sync.</param>
    public RpcPlaintAndCostumePropertySyncMessage(
        MailBox mailbox,
        RpcPropertySyncOperation operation,
        string rpcPropertyPath,
        RpcPropertyContainer val)
        : base(mailbox, operation, rpcPropertyPath, RpcSyncPropertyType.PlaintAndCostume)
    {
        this.Val = val;
    }

    /// <inheritdoc/>
    public override bool MergeKeepOrder(RpcPropertySyncMessage otherMsg)
    {
        if (otherMsg.Operation != RpcPropertySyncOperation.SetValue)
        {
            return false;
        }

        this.Val = (otherMsg as RpcPlaintAndCostumePropertySyncMessage)!.Val;
        return true;
    }

    /// <inheritdoc/>
    public override void MergeIntoSyncInfo(RpcPropertySyncInfo rpcPropertySyncInfo)
    {
        var lastMsg = rpcPropertySyncInfo.GetLastMsg();
        if (lastMsg == null)
        {
            rpcPropertySyncInfo.Enque(this);
        }
        else
        {
            (lastMsg as RpcPlaintAndCostumePropertySyncMessage)!.Val = this.Val;
        }
    }

    /// <inheritdoc/>
    public override PropertySyncCommand Serialize()
    {
        var cmd = new PropertySyncCommand()
        {
            Operation = SyncOperation.SetValue,
        };
        cmd.Args.Add(this.Val.ToRpcArg());
        return cmd;
    }
}