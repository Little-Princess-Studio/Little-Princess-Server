// -----------------------------------------------------------------------
// <copyright file="ClientComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using System;
using System.Collections.Generic;
using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Rpc;

/// <summary>
/// Represents a server component in the Little Princess Studio server.
/// </summary>
public class ClientComponent : ComponentBase
{
    private static readonly HashSet<Type> AllowedRpcPropertyGenTypes = new() { typeof(RpcShadowPlaintProperty<>), typeof(RpcShadowComplexProperty<>) };

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