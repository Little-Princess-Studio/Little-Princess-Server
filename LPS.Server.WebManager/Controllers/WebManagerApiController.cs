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
}