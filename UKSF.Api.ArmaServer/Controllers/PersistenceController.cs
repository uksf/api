using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("persistence")]
[AllowAnonymous]
[LocalhostOnly]
public class PersistenceController(IPersistenceSessionsService persistenceSessionsService, IUksfLogger logger) : ControllerBase
{
    [HttpGet("{key}")]
    public IActionResult Get([FromRoute] string key, [FromQuery] string? format = null)
    {
        logger.LogDebug($"Persistence load requested for key: {key}");

        var session = persistenceSessionsService.Load(key);
        if (session is null)
        {
            return NotFound();
        }

        if (format == "raw")
        {
            var raw = PersistenceConverter.ToHashmap(session);
            return new JsonResult(raw);
        }

        return new JsonResult(session, PersistenceSessionsService.SerializerOptions);
    }
}
