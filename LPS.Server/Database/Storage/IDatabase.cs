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
    /// <param name="connectString">The connection string to use to connect to database.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task<bool> Initialize(string connectString);

    /// <summary>
    /// Shuts down the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous shutdown operation.</returns>
    Task<bool> ShutDown();
}