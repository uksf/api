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

        // Handle HC FPS — separate new entries from existing to avoid multiple Push to the same array field
        var newHeadlessClients = new List<HeadlessClientPerformance>();
        foreach (var hc in headlessClients)
        {
            var existingIndex = session.HeadlessClientPerformance.FindIndex(h => h.Name == hc.Name);
            if (existingIndex >= 0)
            {
                updates.Add(Builders<MissionSession>.Update.PushEach(x => x.HeadlessClientPerformance[existingIndex].Fps, hc.Fps));
            }
            else
            {
                newHeadlessClients.Add(hc);
            }
        }

        if (newHeadlessClients.Count > 0)
        {
            updates.Add(Builders<MissionSession>.Update.PushEach(x => x.HeadlessClientPerformance, newHeadlessClients));
        }

        // Handle player FPS — append samples and extend gaps for absent players
        var reportedPlayerUids = players.Select(p => p.Uid).ToHashSet();

        var newPlayers = new List<PlayerPerformance>();
        foreach (var player in players)
        {
            var existingIndex = session.PlayerPerformance.FindIndex(p => p.Uid == player.Uid);
            if (existingIndex >= 0)
            {
                updates.Add(Builders<MissionSession>.Update.PushEach(x => x.PlayerPerformance[existingIndex].Fps, player.Fps));
            }
            else
            {
                newPlayers.Add(player);
            }

            // Update rolling stats
            await UpdateRollingFpsStatsAsync(sessionId, player.Uid, player.Fps);
        }

        if (newPlayers.Count > 0)
        {
            updates.Add(Builders<MissionSession>.Update.PushEach(x => x.PlayerPerformance, newPlayers));
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
                // Extend existing gap by 5 seconds — use concrete last index
                var lastFpsIndex = existingPlayer.Fps.Count - 1;
                updates.Add(Builders<MissionSession>.Update.Set(x => x.PlayerPerformance[playerIndex].Fps[lastFpsIndex], lastValue - 5));
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

        // Ensure the document exists before atomic update — Min/Max/Inc no-op without a document
        var existing = playerStatsContext.GetSingle(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid);
        if (existing is null)
        {
            var newStats = new PlayerMissionStats
            {
                MissionSessionId = sessionId,
                PlayerUid = playerUid,
                FpsMin = min,
                FpsMax = max,
                FpsSampleCount = count,
                FpsSampleSum = sum
            };
            await playerStatsContext.Add(newStats);
            return;
        }

        var update = Builders<PlayerMissionStats>.Update.Min(x => x.FpsMin, min)
                                                 .Max(x => x.FpsMax, max)
                                                 .Inc(x => x.FpsSampleCount, count)
                                                 .Inc(x => x.FpsSampleSum, sum);

        await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid, update);
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

            // Compute average from rolling totals
            var stats = playerStatsContext.GetSingle(x => x.MissionSessionId == sessionId && x.PlayerUid == player.Uid);
            var average = stats is { FpsSampleCount: > 0 } ? stats.FpsSampleSum / stats.FpsSampleCount : 0;

            var update = Builders<PlayerMissionStats>.Update.Set(x => x.FpsP1, p1).Set(x => x.FpsAverage, average);
            await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == player.Uid, update);
        }
    }
}
