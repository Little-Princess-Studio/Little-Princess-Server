// -----------------------------------------------------------------------
// <copyright file="EnumDispatcher.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using System;
using System.Collections.Generic;
using System.Reflection;
using LPS.Common.Debug;

/// <summary>
/// Generic enum-keyed dispatcher used for the cluster-internal control
/// flows (<see cref="LPS.Common.Rpc.InnerMessages.HostCommandType"/>,
/// <see cref="LPS.Common.Rpc.InnerMessages.ServiceControlMessage"/>, etc.).
/// Replaces hand-rolled <c>switch (enumValue)</c> blocks with a reflection
/// scan that wires every method bearing the matching attribute.
/// <para>
/// Method signature: <c>void Method(TArg arg)</c>. The dispatcher logs
/// registration (Info), each dispatch (Debug), missing handlers (Warn) and
/// exceptions (Error). Duplicate or unknown enum keys throw at startup.
/// </para>
/// </summary>
/// <typeparam name="TKey">Enum type used as the discriminator.</typeparam>
/// <typeparam name="TArg">Single payload type passed to the handler.</typeparam>
public sealed class EnumDispatcher<TKey, TArg>
    where TKey : struct, Enum
{
    private readonly Dictionary<TKey, Entry> handlers = new();
    private readonly string ownerLabel;
    private readonly bool warnOnMissing;

    /// <summary>Initializes a new instance of the <see cref="EnumDispatcher{TKey, TArg}"/> class.</summary>
    /// <param name="ownerLabel">Human-readable label used in log lines.</param>
    /// <param name="warnOnMissing">When true, missing handlers log Warn; otherwise Debug.</param>
    public EnumDispatcher(string ownerLabel, bool warnOnMissing = true)
    {
        this.ownerLabel = ownerLabel;
        this.warnOnMissing = warnOnMissing;
    }

    /// <summary>
    /// Scan <paramref name="owner"/> for methods carrying
    /// <typeparamref name="TAttribute"/> and bind each to its enum key.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute type that exposes a public <c>Key</c> property of <typeparamref name="TKey"/>.</typeparam>
    /// <param name="owner">Object instance whose methods will be invoked.</param>
    public void ScanAndRegister<TAttribute>(object owner)
        where TAttribute : Attribute
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var keyProperty = typeof(TAttribute).GetProperty("Key")
            ?? throw new InvalidOperationException(
                $"[EnumDispatcher:{this.ownerLabel}] attribute {typeof(TAttribute).Name} must expose a public 'Key' property.");

        foreach (var method in owner.GetType().GetMethods(flags))
        {
            var attr = method.GetCustomAttribute(typeof(TAttribute)) as TAttribute;
            if (attr is null)
            {
                continue;
            }

            var key = (TKey)keyProperty.GetValue(attr)!;

            if (this.handlers.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"[EnumDispatcher:{this.ownerLabel}<{typeof(TKey).Name}>] duplicate handler for '{key}': " +
                    $"already bound to {existing.HandlerName}, cannot also bind to {method.DeclaringType!.Name}.{method.Name}.");
            }

            var invoker = BuildInvoker(method, owner);
            this.handlers[key] = new Entry(invoker, $"{method.DeclaringType!.Name}.{method.Name}");
        }

        Logger.Info($"[EnumDispatcher:{this.ownerLabel}<{typeof(TKey).Name}>] registered {this.handlers.Count} handler(s):");
        foreach (var (k, h) in this.handlers)
        {
            Logger.Info($"  {k}  ->  {h.HandlerName}");
        }
    }

    /// <summary>Look up and invoke the handler for <paramref name="key"/>.</summary>
    /// <param name="key">Discriminator.</param>
    /// <param name="arg">Payload passed to the handler.</param>
    public void Dispatch(TKey key, TArg arg)
    {
        if (!this.handlers.TryGetValue(key, out var entry))
        {
            var msg = $"[EnumDispatcher:{this.ownerLabel}<{typeof(TKey).Name}>] no handler for '{key}' (ignored)";
            if (this.warnOnMissing)
            {
                Logger.Warn(msg);
            }
            else
            {
                Logger.Debug(msg);
            }

            return;
        }

        Logger.Debug($"[EnumDispatcher:{this.ownerLabel}<{typeof(TKey).Name}>] dispatch '{key}' -> {entry.HandlerName}");
        try
        {
            entry.Invoke(arg);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"[EnumDispatcher:{this.ownerLabel}<{typeof(TKey).Name}>] {entry.HandlerName} threw on '{key}'");
        }
    }

    private static Action<TArg> BuildInvoker(MethodInfo method, object owner)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(TArg))
        {
            throw new InvalidOperationException(
                $"[EnumDispatcher] {method.DeclaringType!.Name}.{method.Name} must take exactly one parameter of type {typeof(TArg).Name}.");
        }

        if (method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"[EnumDispatcher] {method.DeclaringType!.Name}.{method.Name} must return void.");
        }

        return arg => method.Invoke(owner, new object?[] { arg });
    }

    private sealed record Entry(Action<TArg> Invoke, string HandlerName);
}
