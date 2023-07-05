// -----------------------------------------------------------------------
// <copyright file="MongoDbWrapper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Database.Storage.MongoDb;

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Rpc.InnerMessages;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Represents a MongoDB database implementation of the <see cref="IDatabase"/> interface.
/// </summary>
public class MongoDbWrapper : IDatabase
{
    private enum ComplexKeyType
    {
        String = 0,
        Int = 1,
        MailBox = 2,
    }

    private enum ComplexValueType
    {
        MailBox = 0,
        List = 1,
        Dict = 2,
    }

    /// <summary>
    /// Gets the <see cref="MongoClient"/> instance used by this <see cref="MongoDbWrapper"/> instance.
    /// </summary>
    public MongoClient Client { get; private set; } = null!;

    /// <inheritdoc/>
    public string DefaultDatabaseName => this.defaultDatabaseName;

    private readonly string defaultDatabaseName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbWrapper"/> class.
    /// </summary>
    /// <param name="defaultDatabaseName">The default database name.</param>
    public MongoDbWrapper(string defaultDatabaseName)
    {
        this.defaultDatabaseName = defaultDatabaseName;
    }

    /// <inheritdoc/>
    public Task<bool> Initialize(string connectString)
    {
        this.Client = new MongoClient(connectString);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> ShutDown()
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<Any> LoadEntity(string collectionName, string keyName, string value)
    {
        var coll = this.GetCollection(this.defaultDatabaseName, collectionName);
        var filter = Builders<BsonDocument>.Filter.Eq(keyName, value);
        var queryRes = (await coll.FindAsync(filter)).ToList();

        if (queryRes.Any())
        {
            var entity = queryRes.First();
            return BsonDocumentToAny(entity);
        }

        return Any.Pack(new NullArg());
    }

    /// <inheritdoc/>
    public async Task<bool> SaveEntity(string collectionName, string entityDbId, Any entityValue)
    {
        var coll = this.GetCollection(this.defaultDatabaseName, collectionName);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonObjectId(new ObjectId(entityDbId)));

        var entityDoc = AnyToBsonDocument(entityValue);
        var updateBuilder = Builders<BsonDocument>.Update;
        var combinedUpdate =
            entityDoc.AsQueryable().Select((pair) => updateBuilder.Set(pair.Name, pair.Value));

        var updateRes = await coll.UpdateOneAsync(filter, updateBuilder.Combine(combinedUpdate));
        return updateRes.IsAcknowledged && updateRes.ModifiedCount > 0;
    }

    /// <summary>
    /// Gets the <see cref="IMongoCollection{TDocument}"/> instance for the specified database and collection names.
    /// </summary>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <returns>The <see cref="IMongoCollection{TDocument}"/> instance for the specified database and collection names.</returns>
    public IMongoCollection<BsonDocument> GetCollection(string databaseName, string collectionName)
    {
        return this.Client
                .GetDatabase(databaseName)
                .GetCollection<BsonDocument>(collectionName);
    }

    /// <summary>
    /// Gets the <see cref="IMongoCollection{TDocument}"/> instance for the specified collection name in the default database.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <returns>The <see cref="IMongoCollection{TDocument}"/> instance for the specified collection name in the default database.</returns>
    public IMongoCollection<BsonDocument> GetCollectionFromDefaultDb(string collectionName)
    {
        return this.Client
                .GetDatabase(this.defaultDatabaseName)
                .GetCollection<BsonDocument>(collectionName);
    }

    private static Any BsonDocumentToAny(BsonDocument entity)
    {
        var treeDict = new DictWithStringKeyArg();

        foreach (var elem in entity.AsEnumerable())
        {
            BsonElemToAny(elem, treeDict);
        }

        return Any.Pack(treeDict);
    }

    private static BsonDocument AnyToBsonDocument(Any entity)
    {
        var treeDict = entity.Unpack<DictWithStringKeyArg>();
        var bsonDoc = new BsonDocument();

        foreach (var elem in treeDict.PayLoad)
        {
            var fieldName = elem.Key;
            var value = elem.Value;
            if (value.Is(BoolArg.Descriptor))
            {
                bsonDoc.Add(fieldName, new BsonBoolean(value.Unpack<BoolArg>().PayLoad));
            }
            else if (value.Is(StringArg.Descriptor))
            {
                bsonDoc.Add(fieldName, new BsonString(value.Unpack<StringArg>().PayLoad));
            }
            else if (value.Is(FloatArg.Descriptor))
            {
                bsonDoc.Add(fieldName, new BsonDouble(value.Unpack<FloatArg>().PayLoad));
            }
            else if (value.Is(IntArg.Descriptor))
            {
                bsonDoc.Add(fieldName, new BsonInt32(value.Unpack<IntArg>().PayLoad));
            }
            else if (value.Is(MailBoxArg.Descriptor))
            {
                var mbox = value.Unpack<MailBoxArg>().PayLoad;
                bsonDoc.Add(fieldName, PbMailBoxToBsonDocument(mbox));
            }
            else if (value.Is(ListArg.Descriptor))
            {
                var anyList = value.Unpack<ListArg>().PayLoad;
                var bsonArray = new BsonArray(anyList.Select(x => AnyToBsonDocument(x)));
                var arrayDoc = new BsonDocument()
                {
                    ["$_complex_type"] = (int)ComplexValueType.List,
                    ["data"] = bsonArray,
                };
                bsonDoc.Add(fieldName, arrayDoc);
            }
            else if (value.Is(DictWithIntKeyArg.Descriptor))
            {
                var dict = value.Unpack<DictWithIntKeyArg>().PayLoad;
                var bsonDict = new BsonDocument(
                    dict.ToDictionary(
                        pair => pair.Key.ToString(),
                        pair => AnyToBsonDocument(pair.Value)));
                AddComplexDict(bsonDoc, fieldName, ComplexKeyType.Int, bsonDict);
            }
            else if (value.Is(DictWithMailBoxKeyArg.Descriptor))
            {
                var dict = value.Unpack<DictWithMailBoxKeyArg>().PayLoad;
                var bsonDict = new BsonDocument(
                    dict.ToDictionary(
                        pair => PbMailBoxToString(pair.Key),
                        pair => AnyToBsonDocument(pair.Value)));
                AddComplexDict(bsonDoc, fieldName, valueType: ComplexKeyType.MailBox, bsonDict);
            }
            else if (value.Is(DictWithStringKeyArg.Descriptor))
            {
                var dict = value.Unpack<DictWithStringKeyArg>().PayLoad;
                var bsonDict = new BsonDocument(
                    dict.ToDictionary(
                        pair => pair.Key,
                        pair => AnyToBsonDocument(pair.Value)));
                AddComplexDict(bsonDoc, fieldName, valueType: ComplexKeyType.String, bsonDict);
            }
        }

        return bsonDoc;
    }

    private static void AddComplexDict(BsonDocument bsonDoc, string fieldName, ComplexKeyType valueType, BsonDocument bsonDict)
    {
        bsonDoc.Add(fieldName, new BsonDocument()
        {
            ["$_complex_type"] = (int)ComplexValueType.Dict,
            ["$_key_type"] = (int)valueType,
            ["data"] = bsonDict,
        });
    }

    private static MailBox StringToPbMailBox(string mailboxString)
    {
        var split = mailboxString.Split(':');
        if (split.Length != 4)
        {
            throw new Exception($"Invalid mailbox string: {mailboxString}");
        }

        return new MailBox
        {
            IP = split[0],
            Port = Convert.ToUInt32(split[1]),
            HostNum = Convert.ToUInt32(split[2]),
            ID = split[3],
        };
    }

    private static string PbMailBoxToString(in MailBox mbox)
    {
        return $"{mbox.IP}:{mbox.Port}:{mbox.HostNum}:{mbox.ID}";
    }

    private static BsonDocument PbMailBoxToBsonDocument(in MailBox mbox)
    {
        var bsonValue = new BsonDocument
        {
            ["ip"] = mbox.IP,
            ["port"] = mbox.Port,
            ["hostnum"] = mbox.HostNum,
            ["id"] = mbox.ID,
        };
        return bsonValue;
    }

    private static void BsonElemToAny(BsonElement elem, DictWithStringKeyArg root)
    {
        var keyName = elem.Name;
        var value = elem.Value;

        Any anyValue = BsonValueToAny(value);
        root.PayLoad.Add(keyName, anyValue);
    }

    /*
    For plaint type int/string/float/bool, save as plaint type
    For dict type dict<k, v>, save as stringfy(k): {
        "$_complex_type": "dict",
        "$_key_type": "key type",
        data: { "k": v }
    }
    For lsit type list<e>, save as {
        "$_complex_type": "list",
        data: [e]
    }
    */
    private static Any BsonValueToAny(BsonValue value)
    {
        Any? anyValue = null;
        if (value.IsInt32)
        {
            anyValue = Any.Pack(new IntArg { PayLoad = value.AsInt32 });
        }
        else if (value.IsObjectId)
        {
            anyValue = Any.Pack(new StringArg { PayLoad = value.AsObjectId.ToString() });
        }
        else if (value.IsString)
        {
            anyValue = Any.Pack(new StringArg { PayLoad = value.AsString });
        }
        else if (value.IsBoolean)
        {
            anyValue = Any.Pack(new BoolArg { PayLoad = value.AsBoolean });
        }
        else if (value.IsDouble)
        {
            anyValue = Any.Pack(new FloatArg { PayLoad = (float)value.AsDouble });
        }
        else if (value.IsBsonDocument)
        {
            var doc = value.AsBsonDocument;

            // specified structure dict
            if (doc.TryGetElement("$_complex_type", out var type))
            {
                anyValue = HandleComplexData(anyValue, doc, type);
            }
            else // normal dict
            {
                anyValue = BsonDocumentToAny(doc);
            }
        }

        anyValue ??= Any.Pack(new NullArg());
        return anyValue;
    }

    private static Any? HandleComplexData(Any? anyValue, BsonDocument doc, BsonElement type)
    {
        var complexType = type.Value.AsInt32;
        if (complexType == (uint)ComplexValueType.MailBox)
        {
            var id = doc.GetValue("id").AsString;
            var ip = doc.GetValue("ip").AsString;
            var port = doc.GetValue("port").AsInt32;
            var hostNum = doc.GetValue("hostnum").AsInt32;

            anyValue = Any.Pack(new MailBoxArg
            {
                PayLoad = new MailBox
                {
                    ID = id,
                    IP = ip,
                    Port = (uint)port,
                    HostNum = (uint)hostNum,
                },
            });
        }
        else if (complexType == (uint)ComplexValueType.Dict)
        {
            anyValue = HandleComplexDict(anyValue, doc);
        }
        else if (complexType == (uint)ComplexValueType.List)
        {
            var array = doc.GetValue("data").AsBsonArray;
            var anyArr = new ListArg();
            foreach (var e in array)
            {
                anyArr.PayLoad.Add(BsonValueToAny(e));
            }

            anyValue = Any.Pack(anyArr);
        }

        return anyValue;
    }

    private static Any? HandleComplexDict(Any? anyValue, BsonDocument doc)
    {
        ComplexKeyType keyType = (ComplexKeyType)doc.GetValue("$_key_type").AsInt32;
        var data = doc.GetValue("data").AsBsonDocument;
        if (keyType == ComplexKeyType.MailBox)
        {
            var dict = new DictWithMailBoxKeyArg();
            foreach (var pair in data.AsEnumerable())
            {
                dict.PayLoad.Add(new DictWithMailBoxKeyPair
                {
                    Key = StringToPbMailBox(pair.Name),
                    Value = BsonValueToAny(pair.Value),
                });
            }

            anyValue = Any.Pack(dict);
        }
        else if (keyType == ComplexKeyType.Int)
        {
            var dict = new DictWithIntKeyArg();
            foreach (var pair in data.AsEnumerable())
            {
                dict.PayLoad.Add(Convert.ToInt32(pair.Name), BsonValueToAny(pair.Value));
            }

            anyValue = Any.Pack(dict);
        }
        else if (keyType == ComplexKeyType.String)
        {
            var dict = new DictWithStringKeyArg();

            foreach (var pair in data.AsEnumerable())
            {
                dict.PayLoad.Add(pair.Name, BsonValueToAny(pair.Value));
            }

            anyValue = Any.Pack(dict);
        }

        return anyValue;
    }
}
