// -----------------------------------------------------------------------
// <copyright file="DbApiProviderAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.Attribute;

using System;

/// <summary>
/// Specifies the type of database API provider that a class provides.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class)]
public class DbApiProviderAttribute : System.Attribute
{
    /// <summary>
    /// Gets the type of database API provider that a class provides.
    /// </summary>
    public Type DbType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DbApiProviderAttribute"/> class.
    /// </summary>
    /// <param name="databaseType">database type, should implements <seealso cref="IDatabase"/>. </param>
    public DbApiProviderAttribute(Type databaseType)
    {
        this.DbType = databaseType;
    }
}