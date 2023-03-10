// -----------------------------------------------------------------------
// <copyright file="Player.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Logic.Entity
{
    using LPS.Common.Core.Rpc;
    using LPS.Server.Core.Entity;

    /// <summary>
    /// Player is the real entity between server and client after login process.
    /// </summary>
    [EntityClass]
    public class Player : DistributeEntity
    {
    }
}