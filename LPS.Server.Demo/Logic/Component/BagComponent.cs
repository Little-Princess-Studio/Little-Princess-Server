// -----------------------------------------------------------------------
// <copyright file="BagComponent.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.RpcProperty;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Server.Rpc.RpcProperty;

/// <summary>
/// Represents a component that manages game properties of the <see cref="Player"/> entity.
/// </summary>
public class BagComponent : ComponentBase
{
    /// <summary>
    /// Represents the Hp of the <see cref="Player"/> entity.
    /// </summary>
    [RpcProperty(nameof(GamePropertyComponent.Hp), RpcPropertySetting.Permanent | RpcPropertySetting.ServerToShadow)]
    public readonly RpcList<Item> Hp = new ();

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

/// <summary>
/// Represents an item in the <see cref="BagComponent"/>.
/// </summary>
public class Item : RpcPropertyCostumeContainer<Item>
{
    /// <summary>
    /// Represents the ID of the item in the <see cref="BagComponent"/>.
    /// </summary>
    [RpcProperty]
    public readonly RpcPropertyContainer<int> ItemId = new (0);

    /// <summary>
    /// Represents the name of the item/>.
    /// </summary>
    [RpcProperty]
    public readonly RpcPropertyContainer<string> ItemName = new (string.Empty);

    /// <summary>
    /// Creates a new instance of the <see cref="RpcPropertyContainer"/> class from the specified <see cref="Any"/> content.
    /// </summary>
    /// <param name="content">The content to deserialize.</param>
    /// <returns>A new instance of the <see cref="RpcPropertyContainer"/> class.</returns>
    [RpcPropertyContainerDeserializeEntry]
    public static RpcPropertyContainer FromRpcArg(Any content) => CreateSerializedContainer<Item>(content);

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
