// -----------------------------------------------------------------------
// <copyright file="ClientGlobal.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Demo;

using LPS.Client.Entity;

/// <summary>
/// Client global variables.
/// </summary>
public static class ClientGlobal
{
    /// <summary>
    /// Gets or sets the client shadow entity.
    /// </summary>
    public static ShadowClientEntity ShadowClientEntity { get; set; } = null!;
}