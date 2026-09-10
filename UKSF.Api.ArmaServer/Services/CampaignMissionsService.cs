using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface ICampaignMissionsService
{
    void ApplyDefaults(DomainMission mission);
    DateTime NextStandardOpTimeUtc(DateTime nowUtc);
    MissionDto ToDto(DomainMission mission);
    Task DeleteMission(string id);
    Task<List<ValidationReport>> LaunchMissionAsync(DomainMission mission, string launchedBy);
}

public class CampaignMissionsService(
    IGameServersService gameServersService,
    IMissionsService missionsService,
    ICampaignMissionsContext campaignMissionsContext,
    IIntelPagesContext intelPagesContext,
    IGameServerLaunchService gameServerLaunchService
) : ICampaignMissionsService
{
    private const int StandardOpHourLocal = 19;
    private static readonly TimeZoneInfo LondonZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public void ApplyDefaults(DomainMission mission)
    {
        if (string.IsNullOrEmpty(mission.ServerId))
        {
            mission.ServerId = ResolveMainServerId();
        }

        if (mission.ScheduledTime == default)
        {
            mission.ScheduledTime = NextStandardOpTimeUtc(DateTime.UtcNow);
        }
    }

    public DateTime NextStandardOpTimeUtc(DateTime nowUtc)
    {
        var nowLondon = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, LondonZone);
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)nowLondon.DayOfWeek + 7) % 7;
        var candidate = new DateTime(nowLondon.Year, nowLondon.Month, nowLondon.Day, StandardOpHourLocal, 0, 0, DateTimeKind.Unspecified).AddDays(daysUntilSaturday);
        if (nowLondon >= candidate)
        {
            candidate = candidate.AddDays(7);
        }

        return TimeZoneInfo.ConvertTimeToUtc(candidate, LondonZone);
    }

    public MissionDto ToDto(DomainMission mission)
    {
        return new MissionDto { Mission = mission, MissionFileState = ResolveMissionFileState(mission.MissionName) };
    }

    public async Task DeleteMission(string id)
    {
        await intelPagesContext.DeleteMany(x => x.Scope == IntelScope.Mission && x.OwnerId == id);
        await campaignMissionsContext.Delete(id);
    }

    public async Task<List<ValidationReport>> LaunchMissionAsync(DomainMission mission, string launchedBy)
    {
        var dto = ToDto(mission);
        if (dto.MissionFileState == MissionFileState.Missing)
        {
            throw new BadRequestException("The mission file for this op is missing. Re-assign or restore it before launching.");
        }

        var reports = await gameServerLaunchService.LaunchAsync(mission.ServerId, mission.MissionName, launchedBy);

        mission.LaunchedServerId = mission.ServerId;
        mission.LaunchedMission = mission.MissionName;
        mission.LaunchedAt = DateTime.UtcNow;
        mission.SessionId = null;
        mission.Status = MissionStatus.Scheduled;
        await campaignMissionsContext.Replace(mission);

        return reports;
    }

    private string ResolveMainServerId()
    {
        var servers = gameServersService.GetServers().ToList();
        var main = servers.FirstOrDefault(x => x.Name == "Main Server")
                   ?? servers.FirstOrDefault(x => x.ServerOption == GameServerOption.Singleton)
                   ?? servers.FirstOrDefault();
        return main?.Id;
    }

    private MissionFileState ResolveMissionFileState(string missionName)
    {
        if (string.IsNullOrEmpty(missionName))
        {
            return MissionFileState.Missing;
        }

        return missionsService.FindMissionFilePath(missionName) is null ? MissionFileState.Missing : MissionFileState.Present;
    }
}
