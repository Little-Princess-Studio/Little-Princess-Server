// -----------------------------------------------------------------------
// <copyright file="WebMgrDispatcher.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using System.Collections.Generic;
using System.Reflection;
using LPS.Common.Debug;
using Newtonsoft.Json.Linq;

/// <summary>
/// Per-instance WebManager MQ endpoint. Replaces the hand-rolled
/// <c>if (routingKey == X) { ... } else if ...</c> chains in every
/// <c>*.WebManager.cs</c> partial with a reflection-driven dispatch table.
/// <para>
/// Lifecycle: ctor -&gt; <see cref="ScanAndRegister"/>(owner) -&gt;
/// <see cref="Init"/>(filter). Each instance owns one dispatcher; the
/// underlying <see cref="MessageQueueClient"/> binds the per-instance queue
/// <c>webmgr_que_{name}</c> to <see cref="Consts.WebMgrExchangeName"/> with
/// the supplied routing-key filter (e.g. <c>RoutingKeyToHostManager</c>,
/// <c>RoutingKeyWebManagerToGate</c>).
/// </para>
/// <para>
/// Debug visibility:
/// - registration prints a "ROUTE -&gt; METHOD" table at <c>Info</c>
/// - every dispatch logs <c>route + handler + msgId</c> at <c>Debug</c>
/// - returning <c>null</c> (skip) logs at <c>Debug</c>
/// - handler exceptions log at <c>Error</c> with stack trace
/// - unknown routing keys arriving on the queue log at <c>Debug</c>
/// </para>
/// </summary>
public sealed class WebMgrDispatcher
{
    private readonly MessageQueueClient client = new();
    private readonly Dictionary<string, Handler> handlers = new();
    private readonly string instanceName;
    private readonly string ownerLabel;

    private bool initialised;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMgrDispatcher"/> class.
    /// </summary>
    /// <param name="instanceName">Instance name; used to build the per-instance queue name via <see cref="Consts.GenerateWebManagerQueueName"/>.</param>
    /// <param name="ownerLabel">Human-readable label used in log lines (e.g. <c>"gate0"</c>, <c>"hostmanager"</c>).</param>
    public WebMgrDispatcher(string instanceName, string ownerLabel)
    {
        this.instanceName = instanceName;
        this.ownerLabel = ownerLabel;
    }

    /// <summary>
    /// Reflectively register every method on <paramref name="owner"/> that
    /// carries <see cref="WebMgrHandlerAttribute"/>. Throws if a handler
    /// references a request key that <see cref="WebMgrEndpoints"/> does not
    /// know about - this catches typos at startup rather than at first
    /// request.
    /// </summary>
    /// <param name="owner">Object instance whose methods will be invoked.</param>
    public void ScanAndRegister(object owner)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var methods = owner.GetType().GetMethods(flags);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<WebMgrHandlerAttribute>();
            if (attr is null)
            {
                continue;
            }

            if (!WebMgrEndpoints.RequestToReply.TryGetValue(attr.RequestRoutingKey, out var replyKey))
            {
                throw new InvalidOperationException(
                    $"[WebMgrDispatcher:{this.ownerLabel}] handler {method.DeclaringType!.Name}.{method.Name} " +
                    $"references unknown request key '{attr.RequestRoutingKey}'. Add an entry to WebMgrEndpoints.");
            }

            if (this.handlers.ContainsKey(attr.RequestRoutingKey))
            {
                throw new InvalidOperationException(
                    $"[WebMgrDispatcher:{this.ownerLabel}] duplicate handler for '{attr.RequestRoutingKey}': " +
                    $"already bound to {this.handlers[attr.RequestRoutingKey].HandlerName}, " +
                    $"cannot also bind to {method.DeclaringType!.Name}.{method.Name}.");
            }

            var func = BuildInvoker(method, owner);
            this.handlers[attr.RequestRoutingKey] = new Handler(
                replyKey,
                func,
                $"{method.DeclaringType!.Name}.{method.Name}");
        }

        Logger.Info($"[WebMgrDispatcher:{this.ownerLabel}] registered {this.handlers.Count} handler(s):");
        foreach (var (req, h) in this.handlers)
        {
            Logger.Info($"  {req}  ->  {h.HandlerName}  (reply: {h.ReplyRoutingKey})");
        }
    }

    /// <summary>
    /// Bind the per-instance queue + start consuming. Must be called after
    /// <see cref="ScanAndRegister"/>.
    /// </summary>
    /// <param name="requestRoutingKeyFilter">Routing-key wildcard observed
    /// on <see cref="Consts.WebMgrExchangeName"/>: e.g.
    /// <see cref="Consts.RoutingKeyToHostManager"/>,
    /// <see cref="Consts.RoutingKeyWebManagerToServer"/>,
    /// <see cref="Consts.RoutingKeyWebManagerToGate"/>,
    /// <see cref="Consts.RoutingKeyWebManagerToServiceHost"/>,
    /// <see cref="Consts.RoutingKeyToServiceMgr"/>.</param>
    public void Init(string requestRoutingKeyFilter)
    {
        if (this.initialised)
        {
            return;
        }

        this.initialised = true;

        Logger.Debug($"[WebMgrDispatcher:{this.ownerLabel}] init MQ client, filter={requestRoutingKeyFilter}");
        this.client.Init();
        this.client.AsProducer();
        this.client.AsConsumer();
        this.client.DeclareExchange(Consts.WebMgrExchangeName);
        this.client.DeclareExchange(Consts.ServerExchangeName);
        this.client.BindQueueAndExchange(
            Consts.GenerateWebManagerQueueName(this.instanceName),
            Consts.WebMgrExchangeName,
            requestRoutingKeyFilter);
        this.client.Observe(
            Consts.GenerateWebManagerQueueName(this.instanceName),
            this.Dispatch);
    }

    /// <summary>
    /// Close the underlying MQ client. Safe to call before <see cref="Init"/>.
    /// </summary>
    public void ShutDown()
    {
        if (this.initialised)
        {
            this.client.ShutDown();
        }
    }

    private static Func<JToken, JToken?> BuildInvoker(MethodInfo method, object owner)
    {
        var paramTypes = method.GetParameters();
        if (paramTypes.Length != 1 || paramTypes[0].ParameterType != typeof(JToken))
        {
            throw new InvalidOperationException(
                $"[WebMgrHandler] {method.DeclaringType!.Name}.{method.Name} must take exactly one parameter of type JToken.");
        }

        if (!typeof(JToken).IsAssignableFrom(method.ReturnType))
        {
            throw new InvalidOperationException(
                $"[WebMgrHandler] {method.DeclaringType!.Name}.{method.Name} must return JToken (or a subclass / nullable JToken).");
        }

        return body => (JToken?)method.Invoke(owner, new object[] { body });
    }

    private void Dispatch(string msg, string routingKey)
    {
        if (!this.handlers.TryGetValue(routingKey, out var h))
        {
            Logger.Debug($"[WebMgrDispatcher:{this.ownerLabel}] no handler for routingKey={routingKey} (ignored)");
            return;
        }

        var (msgId, body) = MessageQueueJsonBody.From(msg);
        Logger.Debug($"[WebMgrDispatcher:{this.ownerLabel}] dispatch {routingKey} -> {h.HandlerName} (msgId={msgId})");

        JToken? result;
        try
        {
            result = h.Invoke(body);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[WebMgrDispatcher:{this.ownerLabel}] {h.HandlerName} threw on {routingKey}");
            return;
        }

        if (result is null)
        {
            Logger.Debug($"[WebMgrDispatcher:{this.ownerLabel}] {h.HandlerName} returned null - reply suppressed (msgId={msgId})");
            return;
        }

        var reply = MessageQueueJsonBody.Create(msgId, result);
        this.client.Publish(reply.ToJson(), Consts.ServerExchangeName, h.ReplyRoutingKey);
        Logger.Debug($"[WebMgrDispatcher:{this.ownerLabel}] published reply on {h.ReplyRoutingKey} (msgId={msgId})");
    }

    private sealed record Handler(string ReplyRoutingKey, Func<JToken, JToken?> Invoke, string HandlerName);
}
