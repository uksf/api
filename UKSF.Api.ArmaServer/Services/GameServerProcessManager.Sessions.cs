namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    public async Task<bool> RecordMissionSessionAsync(int apiPort, string sessionId, bool started, DateTime? enqueuedAt, Func<Task> effects)
    {
        if (!started)
        {
            await effects();
        }

        var accepted = true;
        await WithCurrentServerAsync(
            apiPort,
            started ? "mission_started" : "mission_ended",
            async gameServer =>
            {
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
                }

                gameServer.Status.CurrentMissionSessionId = next;
                if (StatusCache.TryGetValue(gameServer.Id, out var cached))
                {
                    cached.CurrentMissionSessionId = next;
                }

                await gameServersContext.Replace(gameServer);
            }
        );
        if (started && accepted)
        {
            await effects();
        }

        return accepted;
    }
}
