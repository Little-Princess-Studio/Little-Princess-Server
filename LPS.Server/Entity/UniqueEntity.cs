// -----------------------------------------------------------------------
// <copyright file="UniqueEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using LPS.Common.Entity;
using LPS.Common.Rpc.RpcStub;

/// <summary>
/// Unique entity should have only 1 instance host-wide.
/// </summary>
[EntityClass]
public class UniqueEntity : BaseEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueEntity"/> class.
    /// </summary>
    protected UniqueEntity()
    {
    }
}