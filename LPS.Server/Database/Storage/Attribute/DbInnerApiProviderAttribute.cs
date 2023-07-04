// -----------------------------------------------------------------------
// <copyright file="DbInnerApiProviderAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.Attribute;

using System;

/// <summary>
/// Specifies the type of database API provider that a class provides.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class)]
public class DbInnerApiProviderAttribute : System.Attribute
{
    /// <summary>
    /// Gets the type of database API provider that a class provides.
    /// </summary>
    public Type DbType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DbInnerApiProviderAttribute"/> class.
    /// </summary>
    /// <param name="databaseType">database type, should implements <seealso cref="IDatabase"/>. </param>
    public DbInnerApiProviderAttribute(Type databaseType)
    {
        this.DbType = databaseType;
    }
}