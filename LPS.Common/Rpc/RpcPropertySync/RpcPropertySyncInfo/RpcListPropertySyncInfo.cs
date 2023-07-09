// -----------------------------------------------------------------------
// <copyright file="RpcListPropertySyncInfo.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;

using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

/// <summary>
/// RPC list property sync info.
/// </summary>
public class RpcListPropertySyncInfo : RpcPropertySyncInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcListPropertySyncInfo"/> class.
    /// </summary>
    /// <param name="isComponentSyncMsg">Whether the sync message is for a component.</param>
    /// <param name="componentName">The name of the component associated with this sync message.</param>
    public RpcListPropertySyncInfo(bool isComponentSyncMsg, string componentName)
        : base(RpcSyncPropertyType.List, isComponentSyncMsg, componentName)
    {
    }

    /// <inheritdoc/>
    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcListPropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}