// -----------------------------------------------------------------------
// <copyright file="GamePropertyComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using LPS.Common.Rpc.RpcProperty;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Represents a component that manages game properties of the <see cref="Player"/> entity.
/// </summary>
public class GamePropertyComponent : ServerComponent
{
    /// <summary>
    /// Represents the Hp of the <see cref="Player"/> entity.
    /// </summary>
    [RpcProperty(nameof(GamePropertyComponent.Hp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public readonly RpcPlaintProperty<int> Hp = new (0);

    /// <summary>
    /// Represents the Sp of the <see cref="Player"/> entity.
    /// </summary>
    [RpcProperty(nameof(GamePropertyComponent.Sp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public readonly RpcPlaintProperty<int> Sp = new (0);

    /// <inheritdoc/>
    public override void OnInit()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override void OnDestory()
    {
        throw new NotImplementedException();
    }
}
