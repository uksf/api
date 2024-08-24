using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Launcher.Models.Parameters;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Clients;
using UKSF.Api.Launcher.Signalr.Hubs;

// ReSharper disable UnusedVariable
// ReSharper disable UnusedParameter.Global
// ReSharper disable NotAccessedField.Local

namespace UKSF.Api.Launcher.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Confirmed, Permissions.Member)]
public class LauncherController(
    IVariablesContext variablesContext,
    IHubContext<LauncherHub, ILauncherClient> launcherHub,
    ILauncherService launcherService,
    ILauncherFileService launcherFileService,
    IHttpContextService httpContextService,
    IDisplayNameService displayNameService,
    IVariablesService variablesService
) : ControllerBase
{
    private readonly IDisplayNameService _displayNameService = displayNameService;
    private readonly IHttpContextService _httpContextService = httpContextService;
    private readonly ILauncherService _launcherService = launcherService;
    private readonly IVariablesService _variablesService = variablesService;

    [HttpGet("update/{platform}/{version}")]
    public void GetUpdate(string platform, string version) { }

    [HttpGet("version")]
    public string GetVersion()
    {
        return variablesContext.GetSingle("LAUNCHER_VERSION").AsString();
    }

    [HttpPost("version")]
    [Permissions(Permissions.Admin)]
    public async Task UpdateVersion([FromBody] UpdateVersionRequest updateVersionRequest)
    {
        await variablesContext.Update("LAUNCHER_VERSION", updateVersionRequest.Version);
        await launcherFileService.UpdateAllVersions();
        await launcherHub.Clients.All.ReceiveLauncherVersion(updateVersionRequest.Version);
    }

    [HttpGet("download/setup")]
    public FileStreamResult GetLauncher()
    {
        return launcherFileService.GetLauncherFile("UKSF Launcher Setup.msi");
    }

    [HttpGet("download/updater")]
    public FileStreamResult GetUpdater()
    {
        return launcherFileService.GetLauncherFile("Updater", "UKSF.Launcher.Updater.exe");
    }

    [HttpPost("download/update")]
    public async Task<FileStreamResult> GetUpdatedFiles([FromBody] GetUpdateFilesRequest getUpdateFilesRequest)
    {
        var updatedFiles = await launcherFileService.GetUpdatedFiles(getUpdateFilesRequest.Files);
        FileStreamResult stream = new(updatedFiles, "application/octet-stream");
        return stream;
    }

    [HttpPost("error")]
    public void ReportError([FromBody] ReportErrorRequest reportErrorRequest)
    {
        var version = reportErrorRequest.Version;
        var message = reportErrorRequest.Message;
        // logger.Log(new LauncherLog(version, message) { userId = httpContextService.GetUserId(), name = displayNameService.GetDisplayName(accountService.GetUserAccount()) });
    }
}
