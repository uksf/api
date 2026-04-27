using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.Modpack.Controllers;

public record TriggerConfigExportRequest(string ModpackVersion);

[Route("modpack/gameconfig")]
public class ConfigExportController(IConfigExportService service, IGameConfigExportsContext context) : ControllerBase
{
    [HttpPost("export")]
    [Permissions(Permissions.Admin)]
    public IActionResult Trigger([FromBody] TriggerConfigExportRequest request)
    {
        var result = service.Trigger(request.ModpackVersion);
        return result.Outcome switch
        {
            TriggerOutcome.Started        => Accepted(new { RunId = result.RunId }),
            TriggerOutcome.AlreadyRunning => Conflict(new { Error = "ConfigExport already running", RunId = result.RunId }),
            _                             => StatusCode(500)
        };
    }

    [HttpGet("export/status")]
    [Permissions(Permissions.Admin)]
    public ConfigExportStatusResponse GetStatus() => service.GetStatus();

    [HttpGet("exports/{id}")]
    [Permissions(Permissions.Admin)]
    public IActionResult GetExport(string id)
    {
        var record = context.GetSingle(id);
        if (record is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(record.FilePath) || !System.IO.File.Exists(record.FilePath))
        {
            return NotFound();
        }

        return PhysicalFile(record.FilePath, "text/plain", Path.GetFileName(record.FilePath));
    }
}
