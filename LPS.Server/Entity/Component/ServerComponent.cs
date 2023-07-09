// -----------------------------------------------------------------------
// <copyright file="ServerComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using System;
using System.Collections.Generic;
using LPS.Common.Rpc;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Represents a server component in the Little Princess Studio server.
/// </summary>
public class ServerComponent : ComponentBase
{
    private static readonly HashSet<Type> AllowedRpcPropertyGenTypes = new() { typeof(RpcPlaintProperty<>), typeof(RpcComplexProperty<>) };

    /// <inheritdoc/>
    public override void OnDestory()
    {
    }

    /// <inheritdoc/>
    public override void OnInit()
    {
    }

    /// <inheritdoc/>
    protected override void InitPropertyTree()
    {
        RpcHelper.BuildPropertyTree(this, AllowedRpcPropertyGenTypes);
    }
}