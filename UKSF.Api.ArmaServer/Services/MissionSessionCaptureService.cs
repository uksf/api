using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IMissionSessionCaptureService
{
    Task CaptureStartedAsync(string serverId, string sessionId);
    Task CaptureEndedAsync(string sessionId);
}

public class MissionSessionCaptureService(ICampaignMissionsContext campaignMissionsContext) : IMissionSessionCaptureService
{
    public async Task CaptureStartedAsync(string serverId, string sessionId)
    {
        if (string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var pending = campaignMissionsContext
                      .Get(x => x.LaunchedServerId == serverId && x.Status == MissionStatus.Scheduled && string.IsNullOrEmpty(x.SessionId))
                      .OrderByDescending(x => x.LaunchedAt)
                      .FirstOrDefault();
        if (pending is null)
        {
            return;
        }

        await campaignMissionsContext.Update(pending.Id, x => x.SessionId, sessionId);
    }

    public async Task CaptureEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var mission = campaignMissionsContext.Get(x => x.SessionId == sessionId).FirstOrDefault();
        if (mission is null)
        {
            return;
        }

        await campaignMissionsContext.Update(mission.Id, x => x.Status, MissionStatus.Complete);
    }
}
