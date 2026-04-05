using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("gameservers/events")]
[AllowAnonymous]
[LocalhostOnly]
public class GameServerEventsController(IGameServerEventHandler eventHandler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveEvent([FromBody] GameServerEvent gameServerEvent)
    {
        await eventHandler.HandleEventAsync(gameServerEvent);
        return Ok();
    }
}
