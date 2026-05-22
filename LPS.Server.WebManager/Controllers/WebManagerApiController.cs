namespace LPS.Server.WebManager.Controllers;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Services;

[ApiController]
[Route("api/web-manager")]
public class WebManagerApiController : Controller
{
    private ServerService serverService;

    public WebManagerApiController(ServerService serverService)
    {
        this.serverService = serverService;
    }

    [HttpGet("server-basic-info")]
    public async Task<IActionResult> ServerBasicInfo()
    {
        var basicInfo = await this.serverService.GetServerBasicInfo();

        var jObjectRes = new JObject
        {
            ["res"] = "Ok",
            ["serverInfo"] = basicInfo,
        };

        return this.Content(jObjectRes.ToString());
    }

    [HttpGet("single-server-info")]
    public async Task<IActionResult> SingleServerInfo(string serverId, int hostNum)
    {
        var detailedInfo = await this.serverService.GetServerDetailedInfo(serverId, hostNum);

        var res = new JObject
        {
            ["res"] = "Ok",
            ["serverDetailedInfo"] = detailedInfo,
        };

        return this.Content(res.ToString());
    }

    [HttpGet("all-entities")]
    public async Task<IActionResult> AllEntities(string serverId, int hostNum)
    {
        var entities = await this.serverService.GetAllEntitiesOfServer(serverId, hostNum);

        var res = new JObject
        {
            ["res"] = "Ok",
            ["entities"] = entities,
        };
        
        return this.Content(res.ToString());
    }

    [HttpGet("all-server-ping-ping-info")]
    public async Task<IActionResult> AllServerPingPingInfo()
    {
        var pingPingInfo = await this.serverService.GetAllServerPingPongInfo();

        var res = new JObject
        {
            ["res"] = "Ok",
            ["srvPingPongInfo"] = pingPingInfo["srvPingPongInfo"],
        };

        return this.Content(res.ToString());
    }

    /// <summary>
    /// One-shot snapshot of every instance the HostManager tracks, grouped by role.
    /// Backs the WebManager cluster overview page.
    /// </summary>
    [HttpGet("cluster-overview")]
    public async Task<IActionResult> ClusterOverview()
    {
        var overview = await this.serverService.GetClusterOverview();

        var res = new JObject
        {
            ["res"] = "Ok",
            ["overview"] = overview,
        };

        return this.Content(res.ToString());
    }

    /// <summary>
    /// Snapshot of the ServiceManager routing map. HostManager does not see
    /// individual service shards (it only tracks the ServiceManager itself),
    /// so this is a separate round-trip that pairs with cluster-overview.
    /// </summary>
    [HttpGet("services-roster")]
    public async Task<IActionResult> ServicesRoster()
    {
        var roster = await this.serverService.GetServicesRoster();

        var res = new JObject
        {
            ["res"] = "Ok",
            ["roster"] = roster,
        };

        return this.Content(res.ToString());
    }

    /// <summary>
    /// Live runtime state of one Gate instance. Backs the Gate detail page.
    /// </summary>
    [HttpGet("gate-detailed-info")]
    public async Task<IActionResult> GateDetailedInfo(string gateId, int hostNum)
    {
        var info = await this.serverService.GetGateDetailedInfo(gateId, hostNum);

        var res = new JObject
        {
            ["res"] = "Ok",
            ["gate"] = info,
        };

        return this.Content(res.ToString());
    }

    /// <summary>
    /// Live runtime state of one service shard. Backs the service shard
    /// detail page.
    /// </summary>
    [HttpGet("service-shard-detailed-info")]
    public async Task<IActionResult> ServiceShardDetailedInfo(string serviceName, uint shard)
    {
        var info = await this.serverService.GetServiceShardDetailedInfo(serviceName, shard);

        var res = new JObject
        {
            ["res"] = "Ok",
            ["shard"] = info,
        };

        return this.Content(res.ToString());
    }
}