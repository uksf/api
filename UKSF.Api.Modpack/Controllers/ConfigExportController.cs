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

    [HttpGet("by-version/{version}")]
    [Permissions(Permissions.Admin)]
    public IActionResult DownloadByVersion(string version)
    {
        var record = context.Get(x => x.ModpackVersion == version && x.Status == ConfigExportStatus.Success)
                            .OrderByDescending(x => x.CompletedAt)
                            .FirstOrDefault();
        if (record is null || string.IsNullOrEmpty(record.FilePath) || !System.IO.File.Exists(record.FilePath))
        {
            return NotFound();
        }

        return PhysicalFile(record.FilePath, "text/plain", $"config_{version}.cpp");
    }

    [HttpGet("available-versions")]
    [Permissions(Permissions.Admin)]
    public string[] GetAvailableVersions()
    {
        return context.Get(x => x.Status == ConfigExportStatus.Success && !string.IsNullOrEmpty(x.FilePath))
                      .Where(x => System.IO.File.Exists(x.FilePath))
                      .Select(x => x.ModpackVersion)
                      .Distinct()
                      .ToArray();
    }
}
