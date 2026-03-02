using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("persistence")]
[AllowAnonymous]
[LocalhostOnly]
public class PersistenceController(IPersistenceSessionsService persistenceSessionsService, IUksfLogger logger) : ControllerBase
{
    // Dedicated options for persistence serialization.
    // Does NOT use DictionaryKeyPolicy (would mutate CustomData keys)
    // Does NOT use InferredTypeConverter (would convert date-like strings to DateTime)
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpGet("{key}")]
    public IActionResult Get([FromRoute] string key)
    {
        logger.LogDebug($"Persistence load requested for key: {key}");

        var session = persistenceSessionsService.Load(key);
        if (session is null)
        {
            return NotFound();
        }

        return new JsonResult(session, SerializerOptions);
    }
}
