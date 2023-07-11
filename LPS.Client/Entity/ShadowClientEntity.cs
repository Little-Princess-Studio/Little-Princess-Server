// -----------------------------------------------------------------------
// <copyright file="ShadowClientEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Entity;

using System.Reflection;
using LPS.Client.Entity.Component;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Entity.Component;
using LPS.Common.Rpc.Attribute;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Util;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// ShadowClientEntity indicates an entity which both have server-side and client-side objects, these two objects
/// has communication where they can call each other and sync properties.
/// </summary>
[EntityClass]
public class ShadowClientEntity : ShadowEntity
{
    /// <summary>
    /// Gets the server proxy.
    /// </summary>
    public ServerProxy Server { get; private set; } = null!;

    /// <summary>
    /// Bind the server mailbox of this entity.
    /// </summary>
    public void BindServerMailBox()
    {
        this.Server = new ServerProxy(this.MailBox, this);
    }

    /// <summary>
    /// Callback when finish transferring.
    /// </summary>
    /// <param name="newMailBox">New mailbox of the entity.</param>
    /// <returns>ValueTask.</returns>
    [RpcMethod(Authority.ClientStub)]
    public ValueTask OnTransfer(MailBox newMailBox)
    {
        Logger.Debug($"entity transferred {newMailBox}");

        this.MailBox = newMailBox;
        this.Server = new ServerProxy(this.MailBox, this);
        return default;
    }

    /// <summary>
    /// Callback when migrated to another entity.
    /// </summary>
    /// <param name="targetMailBox">Target entity mailbox.</param>
    /// <param name="migrateInfo">Migrate info.</param>
    /// <param name="targetEntityClassName">Target entity class name.</param>
    /// <returns>ValueTask.</returns>
    [RpcMethod(Authority.ClientStub)]
    public virtual ValueTask OnMigrated(MailBox targetMailBox, string migrateInfo, string targetEntityClassName)
    {
        RpcClientHelper.RequirePropertyFullSync(targetMailBox.Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitComponents()
    {
        var componentAttrs = this.GetType().GetCustomAttributes<ClientComponentAttribute>();
        foreach (var attr in componentAttrs)
        {
            var componentType = attr.ComponentType;
            var component = (ComponentBase)Activator.CreateInstance(componentType)!;
            var componentName = string.IsNullOrEmpty(componentType.Name) ? attr.ComponentType.Name : componentType.Name;

            component.InitComponent(this, componentName);
            var componentTypeId = TypeIdHelper.GetId(componentType);

            if (this.Components.ContainsKey(componentTypeId))
            {
                Logger.Warn($"Component {componentType.Name} is already added to entity {this.GetType().Name}.");
                continue;
            }

            this.Components.Add(componentTypeId, component);
            this.ComponentNameToComponentTypeId.Add(componentName, componentTypeId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Server proxy of the entity, which indicates the related server-side object.
    /// </summary>
    public class ServerProxy
    {
        /// <summary>
        /// MailBox of the server entity.
        /// </summary>
        public readonly MailBox MailBox;

        /// <summary>
        /// The Owner of this server proxy.
        /// </summary>
        public readonly BaseEntity ClientOwner;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerProxy"/> class.
        /// </summary>
        /// <param name="mailBox">MailBox of the server entity.</param>
        /// <param name="clientOwner">The Owner of this server proxy.</param>
        public ServerProxy(MailBox mailBox, BaseEntity clientOwner)
        {
            this.MailBox = mailBox;
            this.ClientOwner = clientOwner;
        }

        /// <summary>
        /// Start a RPC call to the server.
        /// </summary>
        /// <param name="methodName">Name of the RPC method.</param>
        /// <param name="args">Args list.</param>
        /// <typeparam name="T">Type of the RPC result.</typeparam>
        /// <returns>Result of the RPC.</returns>
        public Task<T> Call<T>(string methodName, params object?[] args)
        {
            return this.ClientOwner.Call<T>(this.MailBox, methodName, RpcType.ClientToServer, args);
        }

        /// <summary>
        /// Start a RPC call to the client.
        /// </summary>
        /// <param name="methodName">Name of the RPC method.</param>
        /// <param name="args">Args list.</param>
        /// <returns>Result of the RPC.</returns>
        public Task Call(string methodName, params object?[] args)
        {
            return this.ClientOwner.Call(this.MailBox, methodName, RpcType.ClientToServer, args);
        }

        /// <summary>
        /// Start a RPC notify to the server, which does not need the response from the client.
        /// </summary>
        /// <param name="methodName">Name of the RPC method.</param>
        /// <param name="args">Args list.</param>
        public void Notify(string methodName, params object?[] args)
        {
            this.ClientOwner.Notify(this.MailBox, methodName, RpcType.ClientToServer, args);
        }
    }
}