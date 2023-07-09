// -----------------------------------------------------------------------
// <copyright file="RpcPlaintAndCostumePropertySyncInfo.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;

using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

/// <summary>
/// RPC property sync info for plaint or costume property.
/// </summary>
public class RpcPlaintAndCostumePropertySyncInfo : RpcPropertySyncInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RpcPlaintAndCostumePropertySyncInfo"/> class.
    /// </summary>
    /// <param name="isComponentSyncMsg">Whether the sync message is for a component.</param>
    /// <param name="componentName">The name of the component associated with this sync message.</param>
    public RpcPlaintAndCostumePropertySyncInfo(bool isComponentSyncMsg, string componentName)
        : base(RpcSyncPropertyType.PlaintAndCostume, isComponentSyncMsg, componentName)
    {
    }

    /// <inheritdoc/>
    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcPlaintAndCostumePropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}