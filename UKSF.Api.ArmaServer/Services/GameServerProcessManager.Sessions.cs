namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    public async Task<bool> RecordMissionSessionAsync(int apiPort, string sessionId, bool started, DateTime? enqueuedAt, Func<Task> effects)
    {
        var accepted = true;
        var handled = await WithCurrentServerAsync(
            apiPort,
            started ? "mission_started" : "mission_ended",
            async gameServer =>
            {
                if (!started)
                {
                    await effects();
                }

                if (PredatesProcess(gameServer, enqueuedAt) || started && (await missionStatsService.GetSessionAsync(sessionId))?.MissionEnded is not null)
                {
                    accepted = false;
                    return;
                }

                var current = gameServer.Status.CurrentMissionSessionId;
                var next = started       ? sessionId :
                    current == sessionId ? null : current;
                if (next == current)
                {
                    return;
                }

                if (started)
                {
                    await missionSessionCaptureService.CaptureStartedAsync(gameServer.Id, sessionId);
                    await effects();
                }

                gameServer.Status.CurrentMissionSessionId = next;
                if (StatusCache.TryGetValue(gameServer.Id, out var cached))
                {
                    cached.CurrentMissionSessionId = next;
                }

                await gameServersContext.Replace(gameServer);
            }
        );
        if (!handled)
        {
            if (started && (await missionStatsService.GetSessionAsync(sessionId))?.MissionEnded is not null)
            {
                return false;
            }

            await effects();
        }

        return accepted;
    }
}
