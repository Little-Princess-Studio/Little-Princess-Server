// -----------------------------------------------------------------------
// <copyright file="IDatabase.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage;

using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;

/// <summary>
/// Database interface.
/// </summary>
public interface IDatabase
{
    /// <summary>
    /// Initializes the database with the specified connection string.
    /// </summary>
    /// <param name="connectString">The connection string to use for the database.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task<bool> Initialize(string connectString);

    /// <summary>
    /// Executes a query against the specified table in the specified database.
    /// </summary>
    /// <param name="databaseName">The name of the database to query.</param>
    /// <param name="tableName">The name of the table to query.</param>
    /// <param name="queryString">The query string to execute.</param>
    /// <returns>A task that represents the asynchronous query operation.</returns>
    Task<IDbDataSet?> QueryOne(string databaseName, string tableName, string queryString);

    /// <summary>
    /// Executes a query against the specified table in the specified database and returns multiple results.
    /// </summary>
    /// <param name="databaseName">The name of the database to query.</param>
    /// <param name="tableName">The name of the table to query.</param>
    /// <param name="queryString">The query string to execute.</param>
    /// <returns>A task that represents the asynchronous query operation and returns a list of <see cref="IDbDataSet"/> objects.</returns>
    Task<List<IDbDataSet>?> QueryMulti(string databaseName, string tableName, string queryString);

    /// <summary>
    /// Returns the native database client object.
    /// </summary>
    /// <returns>The native database client object.</returns>
    object GetRawDbClient();
}