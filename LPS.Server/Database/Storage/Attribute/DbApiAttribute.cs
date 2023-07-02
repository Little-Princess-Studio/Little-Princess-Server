// -----------------------------------------------------------------------
// <copyright file="DbApiAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.Attribute;

using System;

/// <summary>
/// Attribute used to mark a method as a database API method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DbApiAttribute : Attribute
{
}