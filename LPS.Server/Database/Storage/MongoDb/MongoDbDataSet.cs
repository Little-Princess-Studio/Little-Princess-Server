// -----------------------------------------------------------------------
// <copyright file="MongoDbDataSet.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;

/// <summary>
/// Represents a dataset in MongoDB.
/// </summary>
internal readonly struct MongoDbDataSet : IDbDataSet
{
    private readonly BsonDocument doc;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbDataSet"/> struct.
    /// </summary>
    /// <param name="document">.</param>
    public MongoDbDataSet(BsonDocument document)
    {
        this.doc = document;
    }

    /// <inheritdoc/>
    public Dictionary<string, IDbDataSet> AsDict()
    {
        return this.doc.Elements.ToDictionary(
            elem => elem.Name,
            elem => new MongoDbDataSet(elem.Value.AsBsonDocument) as IDbDataSet);
    }

    /// <inheritdoc/>
    public Dictionary<string, T> AsDict<T>(Func<IDbDataSet, T> converter)
    {
        return this.doc.Elements.ToDictionary(
    elem => elem.Name,
    elem => converter.Invoke(new MongoDbDataSet(elem.Value.AsBsonDocument)));
    }

    /// <inheritdoc/>
    public float AsFloat() => (float)this.doc.AsDecimal;

    /// <inheritdoc/>
    public int AsInt() => this.doc.AsInt32;

    /// <inheritdoc/>
    public List<IDbDataSet> AsList() => this.doc.AsBsonArray
            .ToList()
            .ConvertAll(x => new MongoDbDataSet(x.AsBsonDocument) as IDbDataSet);

    /// <inheritdoc/>
    public List<T> AsList<T>(Func<IDbDataSet, T> converter) => this.doc.AsBsonArray
            .ToList()
            .ConvertAll(x => converter.Invoke(new MongoDbDataSet(x.AsBsonDocument)));

    /// <inheritdoc/>
    public string AsString() => this.doc.AsString;

    /// <inheritdoc/>
    public IDbDataSet? FindByDottedPath(string path)
    {
        var route = path.Split(".");
        var root = this.doc;
        foreach (var key in route)
        {
            if (!root.TryGetElement(key, out BsonElement value))
            {
                return null;
            }

            root = value.Value.AsBsonDocument;
        }

        return root == null ? null : new MongoDbDataSet(root);
    }

    /// <inheritdoc/>
    public string ToJson() => this.doc.ToJson();
}