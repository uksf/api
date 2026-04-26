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

        var update = Builders<MissionSession>.Update;

        // Mongo forbids combining a $push to an array with a positional modification of
        // the same array in one update. Add new HC/player rows in their own write first;
        // the second write does serverFps, per-element FPS appends, and gap extensions
        // against the pre-event session state. New rows aren't in `session` here, so
        // FindIndex returns -1 for them and the per-element loop skips them naturally
        // (their Fps was already attached to the row pushed in the first write).
        var newHeadlessClients = headlessClients.Where(hc => session.HeadlessClientPerformance.All(h => h.Name != hc.Name)).ToList();
        var newPlayers = players.Where(p => session.PlayerPerformance.All(existing => existing.Uid != p.Uid)).ToList();

        var additions = new List<UpdateDefinition<MissionSession>>();
        if (newHeadlessClients.Count > 0) additions.Add(update.PushEach(x => x.HeadlessClientPerformance, newHeadlessClients));
        if (newPlayers.Count > 0) additions.Add(update.PushEach(x => x.PlayerPerformance, newPlayers));
        if (additions.Count > 0)
        {
            await sessionsContext.Update(session.Id, update.Combine(additions));
        }

        var updates = new List<UpdateDefinition<MissionSession>>();
        if (serverFps.Count > 0)
        {
            updates.Add(update.PushEach(x => x.ServerFps, serverFps));
        }

        foreach (var hc in headlessClients)
        {
            var i = session.HeadlessClientPerformance.FindIndex(h => h.Name == hc.Name);
            if (i >= 0) updates.Add(update.PushEach(x => x.HeadlessClientPerformance[i].Fps, hc.Fps));
        }

        foreach (var player in players)
        {
            var i = session.PlayerPerformance.FindIndex(p => p.Uid == player.Uid);
            if (i >= 0) updates.Add(update.PushEach(x => x.PlayerPerformance[i].Fps, player.Fps));
            await UpdateRollingFpsStatsAsync(sessionId, player.Uid, player.Fps);
        }

        var reported = players.Select(p => p.Uid).ToHashSet();
        for (var i = 0; i < session.PlayerPerformance.Count; i++)
        {
            var existing = session.PlayerPerformance[i];
            if (reported.Contains(existing.Uid)) continue;

            var lastFps = existing.Fps.Count > 0 ? existing.Fps[^1] : 0;
            updates.Add(
                lastFps < 0
                    ? update.Set(x => x.PlayerPerformance[i].Fps[existing.Fps.Count - 1], lastFps - 5)
                    : update.Push(x => x.PlayerPerformance[i].Fps, -5)
            );
        }

        if (updates.Count > 0)
        {
            await sessionsContext.Update(session.Id, update.Combine(updates));
        }
    }

    private async Task UpdateRollingFpsStatsAsync(string sessionId, string playerUid, List<int> newSamples)
    {
        // Filter out negative gap values to avoid corrupting stats
        var validSamples = newSamples.Where(v => v >= 0).ToList();
        if (validSamples.Count == 0)
        {
            return;
        }

        var min = validSamples.Min();
        var max = validSamples.Max();
        var sum = (double)validSamples.Sum();
        var count = validSamples.Count;

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

            try
            {
                await playerStatsContext.Add(newStats);
                return;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another concurrent call already inserted the document — fall through to update path
            }
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

            if (stats is not null)
            {
                await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == player.Uid, update);
            }
            else
            {
                // Stats document absent (e.g. all samples were gaps filtered by rolling stats) — create it
                await playerStatsContext.Add(
                    new PlayerMissionStats
                    {
                        MissionSessionId = sessionId,
                        PlayerUid = player.Uid,
                        FpsP1 = p1,
                        FpsAverage = average,
                        FpsSampleCount = samples.Count,
                        FpsSampleSum = samples.Sum(),
                        FpsMin = samples[0],
                        FpsMax = samples[^1]
                    }
                );
            }
        }
    }
}
