using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("gameservers/events")]
[AllowAnonymous]
public class GameServerEventsController(IGameServersService gameServersService, IUksfLogger logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveEvent([FromBody] GameServerEvent gameServerEvent)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
        {
            return StatusCode(403, "Only localhost connections are allowed");
        }

        logger.LogDebug($"Received game server event: {gameServerEvent.Type}");
        await gameServersService.HandleGameServerEvent(gameServerEvent);

        return Ok();
    }
}
