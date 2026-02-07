using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Launcher.Models.Parameters;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Clients;
using UKSF.Api.Launcher.Signalr.Hubs;

namespace UKSF.Api.Launcher.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Confirmed, Permissions.Member)]
public class LauncherController(
    IVariablesContext variablesContext,
    IHubContext<LauncherHub, ILauncherClient> launcherHub,
    ILauncherFileService launcherFileService
) : ControllerBase
{
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
        // Stub - launcher sends error reports but processing is not yet implemented
    }
}
