using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IPerformanceService
{
    Task HandlePerformanceEventAsync(string sessionId, List<int> serverFps, List<HeadlessClientPerformance> headlessClients, List<PlayerPerformance> players);

    Task ComputeFinalFpsStatsAsync(string sessionId);
}

public class PerformanceService(IMissionSessionsContext sessionsContext, IPlayerMissionStatsContext playerStatsContext) : IPerformanceService
{
    public async Task HandlePerformanceEventAsync(
        string sessionId,
        List<int> serverFps,
        List<HeadlessClientPerformance> headlessClients,
        List<PlayerPerformance> players
    )
    {
        var session = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (session is null)
        {
            return;
        }

        var updates = new List<UpdateDefinition<MissionSession>>();

        // Append server FPS samples
        if (serverFps.Count > 0)
        {
            updates.Add(Builders<MissionSession>.Update.PushEach(x => x.ServerFps, serverFps));
        }

        // Handle HC FPS
        foreach (var hc in headlessClients)
        {
            var existingIndex = session.HeadlessClientPerformance.FindIndex(h => h.Name == hc.Name);
            if (existingIndex >= 0)
            {
                updates.Add(Builders<MissionSession>.Update.PushEach(x => x.HeadlessClientPerformance[existingIndex].Fps, hc.Fps));
            }
            else
            {
                updates.Add(Builders<MissionSession>.Update.Push(x => x.HeadlessClientPerformance, hc));
            }
        }

        // Handle player FPS — append samples and extend gaps for absent players
        var reportedPlayerUids = players.Select(p => p.Uid).ToHashSet();

        foreach (var player in players)
        {
            var existingIndex = session.PlayerPerformance.FindIndex(p => p.Uid == player.Uid);
            if (existingIndex >= 0)
            {
                updates.Add(Builders<MissionSession>.Update.PushEach(x => x.PlayerPerformance[existingIndex].Fps, player.Fps));
            }
            else
            {
                updates.Add(Builders<MissionSession>.Update.Push(x => x.PlayerPerformance, player));
            }

            // Update rolling stats
            await UpdateRollingFpsStatsAsync(sessionId, player.Uid, player.Fps);
        }

        // Extend gaps for previously seen players absent from this event
        foreach (var existingPlayer in session.PlayerPerformance)
        {
            if (reportedPlayerUids.Contains(existingPlayer.Uid))
            {
                continue;
            }

            // Extend or create gap
            var playerIndex = session.PlayerPerformance.FindIndex(p => p.Uid == existingPlayer.Uid);
            var lastValue = existingPlayer.Fps.Count > 0 ? existingPlayer.Fps[^1] : 0;
            if (lastValue < 0)
            {
                // Extend existing gap by 5 seconds
                updates.Add(Builders<MissionSession>.Update.Set(x => x.PlayerPerformance[playerIndex].Fps[-1], lastValue - 5));
            }
            else
            {
                // Start new gap
                updates.Add(Builders<MissionSession>.Update.Push(x => x.PlayerPerformance[playerIndex].Fps, -5));
            }
        }

        if (updates.Count > 0)
        {
            await sessionsContext.Update(session.Id, Builders<MissionSession>.Update.Combine(updates));
        }
    }

    private async Task UpdateRollingFpsStatsAsync(string sessionId, string playerUid, List<int> newSamples)
    {
        if (newSamples.Count == 0)
        {
            return;
        }

        var min = newSamples.Min();
        var max = newSamples.Max();
        var sum = (double)newSamples.Sum();
        var count = newSamples.Count;

        var update = Builders<PlayerMissionStats>.Update.Min(x => x.FpsMin, min)
                                                 .Max(x => x.FpsMax, max)
                                                 .Inc(x => x.FpsSampleCount, count)
                                                 .Inc(x => x.FpsSampleSum, sum);

        await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid, update);

        // Recompute average from running totals
        var stats = playerStatsContext.GetSingle(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid);
        if (stats is { FpsSampleCount: > 0 })
        {
            var averageUpdate = Builders<PlayerMissionStats>.Update.Set(x => x.FpsAverage, stats.FpsSampleSum / stats.FpsSampleCount);
            await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid, averageUpdate);
        }
    }

    public async Task ComputeFinalFpsStatsAsync(string sessionId)
    {
        var session = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (session is null)
        {
            return;
        }

        foreach (var player in session.PlayerPerformance)
        {
            var samples = player.Fps.Where(v => v >= 0).ToList();
            if (samples.Count == 0)
            {
                continue;
            }

            samples.Sort();
            var p1Index = Math.Max(0, (int)Math.Ceiling(samples.Count * 0.01) - 1);
            var p1 = samples[p1Index];

            var update = Builders<PlayerMissionStats>.Update.Set(x => x.FpsP1, p1);
            await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == player.Uid, update);
        }
    }
}
