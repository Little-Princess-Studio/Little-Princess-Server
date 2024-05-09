// -----------------------------------------------------------------------
// <copyright file="BagComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Entity.Component;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Entity.Component;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Represents a component that manages game properties of the <see cref="Player"/> entity.
/// </summary>
public class BagComponent : ServerComponent
{
    /// <summary>
    /// Represents the item list in the bag of the <see cref="Player"/> entity.
    /// </summary>
    [RpcProperty(nameof(BagComponent.Items), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public readonly RpcComplexProperty<RpcList<Item>> Items = new(new());

    /// <inheritdoc/>
    public override void OnInit()
    {
    }

    /// <inheritdoc/>
    public override void OnDestory()
    {
    }
}

/// <summary>
/// Represents an item in the <see cref="BagComponent"/>.
/// </summary>
public class Item : RpcPropertyCostumeContainer<Item>
{
    /// <summary>
    /// Represents the ID of the item in the <see cref="BagComponent"/>.
    /// </summary>
    [RpcProperty]
    public readonly RpcPropertyContainer<int> ItemId = new(0);

    /// <summary>
    /// Represents the name of the item/>.
    /// </summary>
    [RpcProperty]
    public readonly RpcPropertyContainer<string> ItemName = new(string.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="Item"/> class with the specified ID and name.
    /// </summary>
    /// <param name="itemId">The ID of the item.</param>
    /// <param name="itemName">The name of the item.</param>
    public void Init(int itemId, string itemName)
    {
        this.ItemId.Value = itemId;
        this.ItemName.Value = itemName;
    }
}