// -----------------------------------------------------------------------
// <copyright file="GamePropertyComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo.Entity.Component;

using LPS.Client.Rpc.RpcProperty;
using LPS.Common.Entity.Component;
using LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Represents a component that manages game properties of the <see cref="Player"/> entity.
/// </summary>
public class GamePropertyComponent : ClientComponent
{
    /// <summary>
    /// Represents the Hp of the <see cref="Player"/> entity.
    /// </summary>
    [RpcProperty(nameof(GamePropertyComponent.Hp))]
    public readonly RpcShadowPlaintProperty<int> Hp = new();

    /// <summary>
    /// Represents the Sp of the <see cref="Player"/> entity.
    /// </summary>
    [RpcProperty(nameof(GamePropertyComponent.Sp))]
    public readonly RpcShadowPlaintProperty<int> Sp = new();

    /// <inheritdoc/>
    public override void OnInit()
    {
    }

    /// <inheritdoc/>
    public override void OnDestory()
    {
    }
}