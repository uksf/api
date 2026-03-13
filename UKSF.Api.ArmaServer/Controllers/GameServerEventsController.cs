using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("gameservers/events")]
[AllowAnonymous]
[LocalhostOnly]
public class GameServerEventsController(IGameServersService gameServersService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveEvent([FromBody] GameServerEvent gameServerEvent)
    {
        await gameServersService.HandleGameServerEvent(gameServerEvent);

        return Ok();
    }
}
