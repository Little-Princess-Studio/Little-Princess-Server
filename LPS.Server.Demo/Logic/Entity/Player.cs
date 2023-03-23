// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Demo.Logic.Entity;

using Common.Rpc.Attribute;
using LPS.Server.Entity;

/// <summary>
/// Player is the real entity between server and client after login process.
/// </summary>
[EntityClass]
public class Player : ServerClientEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// </summary>
    /// <param name="desc">Entity description.</param>
    public Player(string desc)
        : base(desc)
    {
    }
}