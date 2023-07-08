// -----------------------------------------------------------------------
// <copyright file="ComponentBase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Entity.Component;

using Google.Protobuf.WellKnownTypes;

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
    /// Gets a value indicating whether this component's data has been loaded.
    /// </summary>
    public bool IsLoaded { get; private set; } = false;

    /// <summary>
    /// Initializes the component with the specified owner and name.
    /// </summary>
    /// <param name="owner">The entity that owns this component.</param>
    /// <param name="name">The name of this component.</param>
    public void Init(BaseEntity owner, string name)
    {
        this.Owner = owner;
        this.Name = name;
    }

    /// <summary>
    /// Loads the component's data from the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task LoadFromDatabase()
    {
        if (this.IsLoaded)
        {
            return Task.CompletedTask;
        }

        if (this.Owner is null)
        {
            throw new Exception("Cannot load a component without an owner.");
        }

        this.OnInit();

        this.IsLoaded = true;
        return Task.CompletedTask;
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
    /// <param name="any">An Any message containing the serialized data.</param>
    public void Deserialize(Any any)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Called when the component's entity is initialized.
    /// </summary>
    public abstract void OnInit();

    /// <summary>
    /// Called when the component's entity is destroyed.
    /// </summary>
    public abstract void OnDestory();
}