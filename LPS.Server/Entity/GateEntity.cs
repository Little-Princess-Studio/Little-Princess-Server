// -----------------------------------------------------------------------
// <copyright file="GateEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using System.Threading.Tasks;
using LPS.Common.Rpc;
using LPS.Common.Rpc.RpcStub;
using LPS.Server.Instance;

/// <summary>
/// Gate entity.
/// </summary>
[EntityClass]
public class GateEntity : ServerEntity
{
    private readonly Gate gate;

    /// <summary>
    /// Initializes a new instance of the <see cref="GateEntity"/> class.
    /// </summary>
    /// <param name="mailbox">MailBox.</param>
    /// <param name="gate">Gate object.</param>
    public GateEntity(MailBox mailbox, Gate gate)
        : base(mailbox)
    {
        this.gate = gate;
        this.IsFrozen = false;
    }

    /// <summary>
    /// Update ServerClientEntity related registration in Gate process.
    /// </summary>
    /// <param name="oldMailBox">Old mailbox.</param>
    /// <param name="newMailBox">New mailbox.</param>
    /// <returns>ValueTask.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public ValueTask UpdateServerClientEntityRegistration(MailBox oldMailBox, MailBox newMailBox)
    {
        this.gate.UpdateServerClientEntityRegistration(oldMailBox, newMailBox);
        return ValueTask.CompletedTask;
    }
}