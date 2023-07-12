// -----------------------------------------------------------------------
// <copyright file="ComponentBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcProperty;

/// <summary>
/// Represents a component that can be attached to an entity.
/// </summary>
public abstract class ComponentBase
{
    /// <summary>
    /// Gets the entity that owns this component.
    /// </summary>
    /// <value>The entity that owns this component.</value>
    public BaseEntity Owner { get; private set; } = null!;

    /// <summary>
    /// Gets the name of this component.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this component's data has been loaded.
    /// </summary>
    public bool IsLoaded { get; protected set; } = false;

    /// <summary>
    /// Gets the property tree for this component.
    /// </summary>
    public Dictionary<string, RpcProperty>? PropertyTree => this.propertyTree;

    private Dictionary<string, RpcProperty>? propertyTree;

    /// <summary>
    /// Sets the property tree for this component.
    /// </summary>
    /// <param name="tree">The property tree to set.</param>
    public void SetPropertyTree(Dictionary<string, RpcProperty> tree)
    {
        this.propertyTree = tree;
    }

    /// <summary>
    /// Initializes the component with the specified owner and name.
    /// </summary>
    /// <param name="owner">The entity that owns this component.</param>
    /// <param name="name">The name of this component.</param>
    public void InitComponent(BaseEntity owner, string name)
    {
        this.Owner = owner;
        this.Name = name;

        this.OnInitPropertyTree();
    }

    /// <summary>
    /// Serializes the component's data to an Any message.
    /// </summary>
    /// <returns>An Any message containing the serialized data.</returns>
    public Any Serialize()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Deserializes the component's data from an Any message.
    /// </summary>
    /// <param name="propertyTree">An Any message containing the serialized data.</param>
    public void Deserialize(Any propertyTree)
    {
        if (propertyTree.Is(descriptor: DictWithStringKeyArg.Descriptor))
        {
            var content = propertyTree.Unpack<DictWithStringKeyArg>();

            foreach (var (key, value) in content.PayLoad)
            {
                if (this.propertyTree!.ContainsKey(key))
                {
                    RpcProperty? prop = this.propertyTree[key];
                    prop.FromProtobuf(value);
                }
                else
                {
                    Debug.Logger.Warn($"Missing sync property {key} in {this.GetType()}");
                }
            }
        }
    }

    /// <summary>
    /// Loads the component's data from the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task OnLoadComponentData()
    {
        if (this.IsLoaded)
        {
            return Task.CompletedTask;
        }

        if (this.Owner is null)
        {
            throw new Exception("Cannot load a component without an owner.");
        }

        this.IsLoaded = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the component's entity is initialized.
    /// </summary>
    public abstract void OnInit();

    /// <summary>
    /// Called when the component's entity is destroyed.
    /// </summary>
    public abstract void OnDestory();

    /// <summary>
    /// Called when initializing the property tree for this component.
    /// </summary>
    protected abstract void OnInitPropertyTree();
}