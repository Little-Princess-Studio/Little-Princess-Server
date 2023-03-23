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
public class Player : DistributeEntity
{
}