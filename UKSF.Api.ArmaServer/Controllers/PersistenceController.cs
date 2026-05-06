using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("persistence")]
[AllowAnonymous]
[LocalhostOnly]
public class PersistenceController(IPersistenceSessionsService persistenceSessionsService, IUksfLogger logger) : ControllerBase
{
    /// <summary>
    /// Emits the persistence session in canonical Arma 3 SQF <c>str</c> format
    /// (a hashmap as <c>[[k,v],...]</c> with string-quoting matching BIS's str()).
    /// The game side runs <c>parseSimpleArray</c> on the response — drops
    /// <c>CBA_fnc_parseJSON</c> from the per-load critical path.
    /// </summary>
    [HttpGet("{key}")]
    public IActionResult Get([FromRoute] string key)
    {
        logger.LogDebug($"Persistence load requested for key: {key}");

        var session = persistenceSessionsService.Load(key);
        if (session is null)
        {
            return NotFound();
        }

        var hashmap = PersistenceConverter.ToHashmap(session);
        var sqf = SqfNotationWriter.Write(hashmap);
        return Content(sqf, "text/plain");
    }
}
