// -----------------------------------------------------------------------
// <copyright file="ServerDefaultCellEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

/// <summary>
/// Default cell entity for server.
/// </summary>
public class ServerDefaultCellEntity : CellEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDefaultCellEntity"/> class.
    /// </summary>
    public ServerDefaultCellEntity()
        : base(string.Empty)
    {
    }

    /// <inheritdoc/>
    public override void Tick()
    {
    }
}