using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Servers)]
public class MissionsController(IMissionsService missionsService, IHubContext<ServersHub, IServersClient> serversHub, IUksfLogger logger) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public List<MissionFile> GetActiveMissions()
    {
        return missionsService.GetActiveMissions();
    }

    [HttpGet("archived")]
    [Authorize]
    public List<MissionFile> GetArchivedMissions()
    {
        return missionsService.GetArchivedMissions();
    }

    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(52428800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    public async Task<MissionsDataset> UploadMissionFile()
    {
        List<MissionReportDataset> missionReports = [];
        try
        {
            foreach (var file in Request.Form.Files.Where(x => x.Length > 0))
            {
                await missionsService.UploadMissionFile(file);
                var missionPatchingResult = await missionsService.PatchMissionFile(file.Name);
                missionPatchingResult.Reports = missionPatchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                missionReports.Add(new MissionReportDataset { Mission = file.Name, Reports = missionPatchingResult.Reports });
                logger.LogAudit($"Uploaded mission '{file.Name}'");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            throw new BadRequestException(exception.Message);
        }

        var missions = missionsService.GetActiveMissions();
        await SendMissionsUpdateIfNotCaller(missions);
        return new MissionsDataset { Missions = missions, MissionReports = missionReports };
    }

    [HttpGet("{fileName}/download")]
    [Authorize]
    public IActionResult DownloadMissionFile(string fileName)
    {
        try
        {
            var stream = missionsService.GetMissionFileStream(fileName);
            return File(stream, "application/octet-stream", fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{fileName}")]
    [Authorize]
    public async Task<IActionResult> DeleteMissionFile(string fileName)
    {
        try
        {
            missionsService.DeleteMissionFile(fileName);
            var missions = missionsService.GetActiveMissions();
            await SendMissionsUpdateIfNotCaller(missions);
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{fileName}/archive")]
    [Authorize]
    public async Task<IActionResult> ArchiveMissionFile(string fileName)
    {
        try
        {
            missionsService.ArchiveMissionFile(fileName);
            var missions = missionsService.GetActiveMissions();
            await SendMissionsUpdateIfNotCaller(missions);
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{fileName}/restore")]
    [Authorize]
    public async Task<IActionResult> RestoreMissionFile(string fileName)
    {
        try
        {
            missionsService.RestoreMissionFile(fileName);
            var missions = missionsService.GetActiveMissions();
            await SendMissionsUpdateIfNotCaller(missions);
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task SendMissionsUpdateIfNotCaller(List<MissionFile> missions)
    {
        if (!HttpContext.Request.Headers.TryGetValue("Hub-Connection-Id", out var connectionId))
        {
            return;
        }

        await serversHub.Clients.All.ReceiveMissionsUpdateIfNotCaller(connectionId, missions);
    }
}
