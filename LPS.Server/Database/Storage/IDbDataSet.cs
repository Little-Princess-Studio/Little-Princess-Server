// -----------------------------------------------------------------------
// <copyright file="IDbDataSet.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage;

using System;
using System.Collections.Generic;

/// <summary>
/// IDatabaseDataSet is an interface type to represent the database's query result.
/// LPS will only use this type to use the database query result regardless of the real
/// database (MySql/MongoDB/etc.)
/// </summary>
public interface IDbDataSet
{
    /// <summary>
    /// Finds an IDbDataSet object by its path.
    /// </summary>
    /// <param name="path">The path of the IDbDataSet object to find.</param>
    /// <returns>The IDbDataSet object found by its path.</returns>
    IDbDataSet? FindByDottedPath(string path);

    /// <summary>
    /// Converts the IDbDataSet object to an integer.
    /// </summary>
    /// <returns>The integer representation of the IDbDataSet object.</returns>
    int AsInt();

    /// <summary>
    /// Converts the IDbDataSet object to a float.
    /// </summary>
    /// <returns>The float representation of the IDbDataSet object.</returns>
    float AsFloat();

    /// <summary>
    /// Converts the IDbDataSet object to a string.
    /// </summary>
    /// <returns>The string representation of the IDbDataSet object.</returns>
    string AsString();

    /// <summary>
    /// Converts the IDbDataSet object to a list of IDbDataSet objects.
    /// </summary>
    /// <returns>The list of IDbDataSet objects.</returns>
    List<IDbDataSet> AsList();

    /// <summary>
    /// Converts the IDbDataSet object to a list of objects of type T.
    /// </summary>
    /// <typeparam name="T">The type of object to convert the IDbDataSet object to.</typeparam>
    /// <param name="converter">The function to convert the IDbDataSet object to an object of type T.</param>
    /// <returns>The list of objects of type T.</returns>
    List<T> AsList<T>(Func<IDbDataSet, T> converter);

    /// <summary>
    /// Converts the IDbDataSet object to a dictionary of string keys and IDbDataSet values.
    /// </summary>
    /// <returns>The dictionary of string keys and IDbDataSet values.</returns>
    Dictionary<string, IDbDataSet> AsDict();

    /// <summary>
    /// Converts the IDbDataSet object to a dictionary of string keys and objects of type T values.
    /// </summary>
    /// <typeparam name="T">The type of object to convert the IDbDataSet object to.</typeparam>
    /// <param name="converter">The function to convert the IDbDataSet object to an object of type T.</param>
    /// <returns>The dictionary of string keys and objects of type T values.</returns>
    Dictionary<string, T> AsDict<T>(Func<IDbDataSet, T> converter);

    /// <summary>
    /// Returns the JSON representation of the IDbDataSet object.
    /// </summary>
    /// <returns>The JSON representation of the IDbDataSet object.</returns>
    string ToJson();
}
