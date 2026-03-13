using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("gameservers/events")]
[AllowAnonymous]
[LocalhostOnly]
public class GameServerEventsController(IGameServersService gameServersService, IUksfLogger logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveEvent([FromBody] GameServerEvent gameServerEvent)
    {
        logger.LogDebug($"Received game server event: {gameServerEvent.Type}");

        await gameServersService.HandleGameServerEvent(gameServerEvent);

        return Ok();
    }
}
