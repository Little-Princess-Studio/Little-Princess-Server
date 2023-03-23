// -----------------------------------------------------------------------
// <copyright file="RpcListPropertySyncInfo.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Core.Rpc.RpcPropertySync.RpcPropertySyncInfo;

/// <summary>
/// RPC list property sync info.
/// </summary>
public class RpcListPropertySyncInfo : RpcPropertySyncInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcListPropertySyncInfo"/> class.
    /// </summary>
    public RpcListPropertySyncInfo()
        : base(RpcSyncPropertyType.List)
    {
    }

    /// <inheritdoc/>
    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcListPropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}