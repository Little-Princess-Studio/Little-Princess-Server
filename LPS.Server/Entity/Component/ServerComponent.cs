// -----------------------------------------------------------------------
// <copyright file="ServerComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Common.Rpc;
using LPS.Server.Database;
using LPS.Server.Entity;
using LPS.Server.Rpc;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Represents a server component in the Little Princess Studio server.
/// </summary>
public class ServerComponent : ComponentBase
{
    /// <inheritdoc/>
    public override void OnDestory()
    {
    }

    /// <inheritdoc/>
    public override void OnInit()
    {
    }

    /// <inheritdoc/>
    public override async Task OnLoadComponentData()
    {
        if (this.IsLoaded)
        {
            return;
        }

        if (this.Owner is not DistributeEntity)
        {
            await base.OnLoadComponentData();
            return;
        }

        var distEntity = (this.Owner as DistributeEntity)!;

        if (!distEntity.IsDatabaseEntity)
        {
            await base.OnLoadComponentData();
            return;
        }

        var collName = distEntity.GetCollectionName()!;
        var databasebId = distEntity.DbId!;
        var compName = this.Name!;
        var compData = await DbHelper.CallDbInnerApi(
            "LoadComponent",
            RpcHelper.GetRpcAny(collName),
            RpcHelper.GetRpcAny(databasebId),
            RpcHelper.GetRpcAny(compName));

        this.Deserialize(compData);

        this.IsLoaded = true;
    }

    /// <inheritdoc/>
    protected override void OnInitPropertyTree()
    {
        RpcHelper.BuildPropertyTree(this, RpcServerHelper.AllowedRpcPropertyGenTypes);
    }
}