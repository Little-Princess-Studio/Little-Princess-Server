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
}