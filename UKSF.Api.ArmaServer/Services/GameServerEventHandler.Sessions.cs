using System.Globalization;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerEventHandler
{
    private async Task HandleMissionLifecycleEvent(int apiPort, Dictionary<string, object> data, bool isStart, bool hasNoGameServerRow)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        var tracked = !hasNoGameServerRow && gameServer is not null;
        var now = DateTime.UtcNow;

        if (isStart)
        {
            var mission = data.TryGetValue("mission", out var missionValue) ? missionValue.ToString() : string.Empty;
            var map = data.TryGetValue("map", out var mapValue) ? mapValue.ToString() : string.Empty;
            Func<Task> start = async () =>
            {
                if (string.IsNullOrEmpty(mission) || string.IsNullOrEmpty(map))
                {
                    return;
                }

                npcTraceMissions?.Started(sessionId, mission, map, gameServer?.Name);
                await missionStatsService.HandleMissionStartedAsync(sessionId, mission, map, now);
            };

            var accepted = tracked
                ? await processManager.RecordMissionSessionAsync(apiPort, sessionId, true, EnqueueTime(data), start)
                : (await missionStatsService.GetSessionAsync(sessionId))?.MissionEnded is null;
            if (!accepted)
            {
                logger.LogWarning($"Ignoring mission_started for session '{sessionId}': it already ended or predates the server process");
                return;
            }

            if (!tracked)
            {
                await start();
            }
        }
        else
        {
            var duration = data.TryGetValue("duration", out var durationValue) && double.TryParse(durationValue?.ToString(), out var parsed) ? parsed : 0;
            Func<Task> end = () => missionStatsService.HandleMissionEndedAsync(sessionId, duration, EnqueueTimeOr(data, now));
            if (tracked)
            {
                await processManager.RecordMissionSessionAsync(apiPort, sessionId, false, EnqueueTime(data), end);
            }
            else
            {
                await end();
            }

            await missionSessionCaptureService.CaptureEndedAsync(sessionId);
            await npcBrokerService.HandleMissionEndedAsync(sessionId);
        }
    }

    private static DateTime EnqueueTimeOr(Dictionary<string, object> data, DateTime fallback) => EnqueueTime(data) ?? fallback;

    private static DateTime? EnqueueTime(Dictionary<string, object> data) =>
        data.TryGetValue("enqueueAt", out var value) &&
        value is string text &&
        DateTime.TryParse(text, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
}
