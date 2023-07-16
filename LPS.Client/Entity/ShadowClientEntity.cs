// -----------------------------------------------------------------------
// <copyright file="ShadowClientEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Entity;

using System.Collections.ObjectModel;
using System.Reflection;
using LPS.Client.Entity.Component;
using LPS.Client.Rpc;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Entity.Component;
using LPS.Common.Rpc.InnerMessages;
using LPS.Common.Rpc.RpcStub;
using LPS.Common.Util;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// ShadowClientEntity indicates an entity which both have server-side and client-side objects, these two objects
/// has communication where they can call each other and sync properties.
/// </summary>
[EntityClass]
[RpcStubGenerator(typeof(RpcStubForShadowClientEntityGenerator))]
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

    /// <summary>
    /// Initializes all components of the client entity.
    /// All the components of the client entity are lazy-loaded, so `OnInit` will not be invoked here.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public override Task InitComponents()
    {
        var components = new Dictionary<uint, ComponentBase>();
        var componentNameToComponentTypeId = new Dictionary<string, uint>();

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

            components.Add(componentTypeId, component);
            componentNameToComponentTypeId.Add(componentName, componentTypeId);
        }

        this.Components = new ReadOnlyDictionary<uint, ComponentBase>(components);
        this.ComponentNameToComponentTypeId = new ReadOnlyDictionary<string, uint>(componentNameToComponentTypeId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the component of the specified type from the entity.
    /// </summary>
    /// <param name="componentType">The type of component to get. If the component is marked as `LazyLoad`, it will be loaded this time.</param>
    /// <returns>The component of the specified type.</returns>
    public override async ValueTask<ComponentBase> GetComponent(System.Type componentType)
    {
        var typeId = TypeIdHelper.GetId(componentType);
        var component = await this.GetComponentInternal(typeId);
        return component;
    }

    /// <summary>
    /// Gets the component with the specified name from the entity.
    /// </summary>
    /// <param name="componentName">The name of the component to get. If the component is marked as `LazyLoad`, it will be loaded this time.</param>
    /// <returns>The component with the specified name.</returns>
    public override async ValueTask<ComponentBase> GetComponent(string componentName)
    {
        if (!this.ComponentNameToComponentTypeId.ContainsKey(componentName))
        {
            var e = new Exception($"Component {componentName} not found.");
            Logger.Error(e);
            throw e;
        }

        var typeId = this.ComponentNameToComponentTypeId[componentName];
        var component = await this.GetComponentInternal(typeId);
        return component;
    }

    private async ValueTask<ComponentBase> GetComponentInternal(uint typeId)
    {
        if (!this.Components.ContainsKey(typeId))
        {
            var e = new Exception($"Component not found.");
            Logger.Error(e);
            throw e;
        }

        var component = this.Components[typeId];

        if (!component.IsLoaded)
        {
            await component.OnLoadComponentData();
        }

        return component;
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