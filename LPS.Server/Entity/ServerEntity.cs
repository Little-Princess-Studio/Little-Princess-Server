// -----------------------------------------------------------------------
// <copyright file="ServerEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Entity;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPS.Common.Debug;
using LPS.Common.Rpc.Attribute;
using MailBox = LPS.Common.Rpc.MailBox;

/// <summary>
/// Server entity indicates entity only exists on server.
/// </summary>
[EntityClass]
public class ServerEntity : UniqueEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerEntity"/> class.
    /// </summary>
    /// <param name="mailbox">Mailbox of the server entity.</param>
    public ServerEntity(MailBox mailbox)
    {
        this.MailBox = mailbox;
    }

    /// <summary>
    /// Rpc method for Echo test.
    /// </summary>
    /// <param name="msg">Echo message.</param>
    [RpcMethod(Authority.All)]
    public void Echo(string msg)
    {
        Logger.Info($"Echo Echo Echo {msg}");
    }

    /// <summary>
    /// Rpc method for Echo test.
    /// </summary>
    /// <param name="testMap">Dict rpc arg.</param>
    /// <param name="testInt">Int rpc arg.</param>
    /// <param name="testFloat">Float rpc arg.</param>
    /// <param name="testList">List rpc arg.</param>
    [RpcMethod(Authority.ServerOnly)]
    public void Echo2(Dictionary<string, string> testMap, int testInt, float testFloat, List<string> testList)
    {
        Logger.Info("Echo Echo Echo");
        Logger.Info($"{testInt}");
        Logger.Info($"{testFloat}");
        Logger.Info($"{string.Join(',', testMap.Select(pair => '<' + pair.Key + ',' + pair.Value + '>'))}");
        Logger.Info($"{string.Join(',', testList)}");
    }

    /// <summary>
    /// Rpc method for Echo test.
    /// </summary>
    /// <param name="mb">Mailbox of other entity.</param>
    /// <returns>ValueTask.</returns>
    [RpcMethod(Authority.ServerOnly)]
    public ValueTask Echo3(MailBox mb)
    {
        Logger.Info($"Got mailbox {mb}");

        // await this.Call(mb, "Hello, LPS");
        return default(ValueTask);
    }
}