using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IOpsService
{
    void ApplyDefaults(DomainOp op);
    DateTime NextStandardOpTimeUtc(DateTime nowUtc);
    OpDto ToDto(DomainOp op);
    Task DeleteOp(string id);
}

public class OpsService(
    IGameServersService gameServersService,
    IMissionsService missionsService,
    IOpsContext opsContext,
    IIntelPagesContext intelPagesContext
) : IOpsService
{
    private const int StandardOpHourLocal = 19;
    private static readonly TimeZoneInfo LondonZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public void ApplyDefaults(DomainOp op)
    {
        if (string.IsNullOrEmpty(op.ServerId))
        {
            op.ServerId = ResolveMainServerId();
        }

        if (op.ScheduledTime == default)
        {
            op.ScheduledTime = NextStandardOpTimeUtc(DateTime.UtcNow);
        }
    }

    public DateTime NextStandardOpTimeUtc(DateTime nowUtc)
    {
        var nowLondon = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, LondonZone);
        var candidate = new DateTime(nowLondon.Year, nowLondon.Month, nowLondon.Day, StandardOpHourLocal, 0, 0, DateTimeKind.Unspecified);
        if (nowLondon >= candidate)
        {
            candidate = candidate.AddDays(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(candidate, LondonZone);
    }

    public OpDto ToDto(DomainOp op)
    {
        return new OpDto { Op = op, MissionFileState = ResolveMissionFileState(op.MissionName) };
    }

    public async Task DeleteOp(string id)
    {
        await intelPagesContext.DeleteMany(x => x.Scope == IntelScope.Op && x.OwnerId == id);
        await opsContext.Delete(id);
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
