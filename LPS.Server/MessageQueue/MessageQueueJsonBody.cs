// -----------------------------------------------------------------------
// <copyright file="MessageQueueJsonBody.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.MessageQueue;

using LPS.Common.Debug;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Common json body to send message inside message queue.
/// </summary>
public class MessageQueueJsonBody
{
    /// <summary>
    /// Gets or sets rpc id of json.
    /// </summary>
    [JsonProperty("rpcId")]
    public uint RpcId { get; set; }

    /// <summary>
    /// Gets or sets or the body content of json.
    /// </summary>
    [JsonProperty("body")]
    public JObject Body { get; set; }

    /// <summary>
    /// Get info from body string.
    /// </summary>
    /// <param name="body">Body json string.</param>
    /// <returns>(Rpc id, JToken body) pair.</returns>
    public static (uint RpcId, JObject Body) From(string body)
    {
        var rawBody = JsonConvert.DeserializeObject<MessageQueueJsonBody>(body);
        var rpcId = rawBody !.RpcId;
        var jsonBody = rawBody.Body;
        return (rpcId, jsonBody);
    }

    /// <summary>
    /// Create a body.
    /// </summary>
    /// <param name="rpcId">Rpc id.</param>
    /// <param name="obj">Obj to convert to string body content.</param>
    /// <returns>Instance of MessageQueueJsonBody.</returns>
    public static MessageQueueJsonBody Create(uint rpcId, object obj)
    {
        var body = JObject.FromObject(obj);
        return new MessageQueueJsonBody(rpcId, body);
    }

    /// <summary>
    /// Convert to json string.
    /// </summary>
    /// <returns>Json string.</returns>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this);
    }

    [JsonConstructor]
    private MessageQueueJsonBody(uint rpcId, JObject body)
    {
        this.RpcId = rpcId;
        this.Body = body;
    }
}