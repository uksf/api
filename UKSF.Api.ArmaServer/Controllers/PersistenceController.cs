using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("persistence")]
[AllowAnonymous]
public class PersistenceController(IPersistenceSessionsService persistenceSessionsService, IUksfLogger logger) : ControllerBase
{
    [HttpGet("{key}")]
    public ActionResult<DomainPersistenceSession> Get([FromRoute] string key)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
        {
            return StatusCode(403, "Only localhost connections are allowed");
        }

        logger.LogDebug($"Persistence load requested for key: {key}");

        var session = persistenceSessionsService.Load(key);
        if (session is null)
        {
            return NotFound();
        }

        return session;
    }
}
