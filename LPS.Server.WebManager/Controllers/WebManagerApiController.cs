namespace LPS.Server.WebManager.Controllers;

using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("server-info")]
    public async Task<IActionResult> ServerInfo()
    {
        var cnt = await this.serverService.GetServerCnt();
        return this.Ok(new
        {
            res = "Ok",
            serverCnt = cnt,
        });
    }
}