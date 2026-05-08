using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;

namespace UKSF.Api.Modpack.Controllers;

public record TriggerGameDataExportRequest(string ModpackVersion = null);

[Route("modpack/gamedata")]
public class GameDataExportController(
    IGameDataExportService service,
    IGameDataExportsContext context,
    IVariablesService variablesService,
    IReleasesContext releasesContext
) : ControllerBase
{
    private static readonly System.Buffers.SearchValues<char> UnsafeVersionChars = System.Buffers.SearchValues.Create("/\\:\0");

    private static bool IsSafeVersionSegment(string version) =>
        !string.IsNullOrWhiteSpace(version) && version.AsSpan().IndexOfAny(UnsafeVersionChars) < 0 && version != "." && version != "..";

    private record FileSpec(string Variable, Func<DomainGameDataExport, bool> Has, string Prefix, string Extension, string ContentType);

    private static readonly Dictionary<string, FileSpec> FileMap = new()
    {
        ["config"] = new("SERVER_PATH_CONFIG_EXPORT", d => d.HasConfig, "config", "cpp", "text/plain"),
        ["cba-settings"] = new("SERVER_PATH_SETTINGS_EXPORT", d => d.HasCbaSettings, "cba_settings", "sqf", "text/plain"),
        ["cba-settings-reference"] = new(
            "SERVER_PATH_SETTINGS_EXPORT",
            d => d.HasCbaSettingsReference,
            "cba_settings_reference",
            "json",
            "application/json"
        )
    };

    [HttpPost("export")]
    [Permissions(Permissions.Admin)]
    public IActionResult Trigger([FromBody] TriggerGameDataExportRequest request)
    {
        var version = !string.IsNullOrWhiteSpace(request?.ModpackVersion)
            ? request.ModpackVersion
            : releasesContext.Get().Where(x => !x.IsDraft).OrderByDescending(x => x.Timestamp).FirstOrDefault()?.Version;

        if (string.IsNullOrWhiteSpace(version))
        {
            return BadRequest(new { Error = "No released modpack version available" });
        }

        var result = service.Trigger(version);
        return result.Outcome switch
        {
            TriggerOutcome.Started        => Accepted(new { RunId = result.RunId, Version = version }),
            TriggerOutcome.AlreadyRunning => Conflict(new { Error = "GameDataExport already running", RunId = result.RunId }),
            _                             => StatusCode(500)
        };
    }

    [HttpGet("export/status")]
    [Permissions(Permissions.Admin)]
    public GameDataExportStatusResponse GetStatus() => service.GetStatus();

    [HttpGet]
    [Permissions(Permissions.Admin)]
    public IEnumerable<DomainGameDataExport> List() => context.Get();

    [HttpGet("{version}/{file}")]
    [Permissions(Permissions.Admin)]
    public IActionResult Download(string version, string file)
    {
        if (!FileMap.TryGetValue(file, out var spec))
        {
            return NotFound();
        }

        if (!IsSafeVersionSegment(version))
        {
            return NotFound();
        }

        var record = context.Get(d => d.ModpackVersion == version).OrderByDescending(d => d.CompletedAt).FirstOrDefault();
        if (record is null || !spec.Has(record))
        {
            return NotFound();
        }

        var root = variablesService.GetVariable(spec.Variable).AsString();
        var fileName = $"{spec.Prefix}_{version}.{spec.Extension}";
        var path = Path.Combine(root, fileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, spec.ContentType, fileName);
    }
}
