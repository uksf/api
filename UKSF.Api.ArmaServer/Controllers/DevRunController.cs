using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

public record TriggerDevRunRequest(string Sqf, IReadOnlyList<string> Mods, int? TimeoutSeconds);

[Route("dev-run")]
public class DevRunController(IDevRunService service, IDevRunsContext context) : ControllerBase
{
    [HttpPost("")]
    [Permissions(Permissions.Admin)]
    public IActionResult Trigger([FromBody] TriggerDevRunRequest request)
    {
        var result = service.Trigger(request.Sqf, request.Mods, request.TimeoutSeconds);
        return result.Outcome switch
        {
            DevRunTriggerOutcome.Started        => Accepted(new { result.RunId }),
            DevRunTriggerOutcome.AlreadyRunning => Conflict(new { Error = "AlreadyRunning", result.RunId }),
            DevRunTriggerOutcome.BadModPaths    => BadRequest(new { Error = "BadModPaths", MissingPaths = result.MissingPaths }),
            _                                   => StatusCode(500)
        };
    }

    [HttpGet("{runId}/status")]
    [Permissions(Permissions.Admin)]
    public IActionResult GetStatus(string runId)
    {
        var status = service.GetStatus(runId);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("{runId}")]
    [Permissions(Permissions.Admin)]
    public IActionResult Get(string runId)
    {
        var record = context.GetSingle(x => x.RunId == runId);
        if (record is null) return NotFound();
        if (!string.IsNullOrEmpty(record.ResultFilePath) && System.IO.File.Exists(record.ResultFilePath))
        {
            return PhysicalFile(record.ResultFilePath, "text/plain", $"{runId}.txt");
        }

        return Ok(record);
    }
}
