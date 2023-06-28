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
    public RpcPlaintAndCostumePropertySyncInfo()
        : base(RpcSyncPropertyType.PlaintAndCostume)
    {
    }

    /// <inheritdoc/>
    public override void AddNewSyncMessage(RpcPropertySyncMessage msg)
    {
        var newMsg = (msg as RpcPlaintAndCostumePropertySyncMessage)!;
        newMsg.MergeIntoSyncInfo(this);
    }
}