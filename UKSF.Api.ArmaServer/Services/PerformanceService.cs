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
        var session = sessionsContext.FindFirst(s => s.SessionId == sessionId);
        if (session is null)
        {
            return;
        }

        await AddNewRowsInTheirOwnWriteAsync(session, headlessClients, players);
        await AppendReportedFpsToExistingRowsAsync(session, serverFps, headlessClients, players);
        await ExtendGapsForAbsentPlayersInTheirOwnWriteAsync(session, players);
    }

    private async Task AddNewRowsInTheirOwnWriteAsync(MissionSession session, List<HeadlessClientPerformance> headlessClients, List<PlayerPerformance> players)
    {
        var update = Builders<MissionSession>.Update;
        var newHeadlessClients = headlessClients.Where(hc => session.HeadlessClientPerformance.All(h => h.Name != hc.Name)).ToList();
        var newPlayers = players.Where(p => session.PlayerPerformance.All(existing => existing.Uid != p.Uid)).ToList();

        var additions = new List<UpdateDefinition<MissionSession>>();
        if (newHeadlessClients.Count > 0) additions.Add(update.PushEach(x => x.HeadlessClientPerformance, newHeadlessClients));
        if (newPlayers.Count > 0) additions.Add(update.PushEach(x => x.PlayerPerformance, newPlayers));
        if (additions.Count > 0)
        {
            await sessionsContext.Update(session.Id, update.Combine(additions));
        }
    }

    private async Task AppendReportedFpsToExistingRowsAsync(
        MissionSession session,
        List<int> serverFps,
        List<HeadlessClientPerformance> headlessClients,
        List<PlayerPerformance> players
    )
    {
        var update = Builders<MissionSession>.Update;
        var pushes = new List<UpdateDefinition<MissionSession>>();
        if (serverFps.Count > 0)
        {
            pushes.Add(update.PushEach(x => x.ServerFps, serverFps));
        }

        foreach (var hc in headlessClients)
        {
            var i = session.HeadlessClientPerformance.FindIndex(h => h.Name == hc.Name);
            if (i >= 0) pushes.Add(update.PushEach(x => x.HeadlessClientPerformance[i].Fps, hc.Fps));
        }

        foreach (var player in players)
        {
            var i = session.PlayerPerformance.FindIndex(p => p.Uid == player.Uid);
            if (i >= 0) pushes.Add(update.PushEach(x => x.PlayerPerformance[i].Fps, player.Fps));
        }

        if (pushes.Count > 0)
        {
            await sessionsContext.Update(session.Id, update.Combine(pushes));
        }
    }

    private async Task ExtendGapsForAbsentPlayersInTheirOwnWriteAsync(MissionSession session, List<PlayerPerformance> players)
    {
        var update = Builders<MissionSession>.Update;
        var reported = players.Select(p => p.Uid).ToHashSet();
        var gapExtensions = new List<UpdateDefinition<MissionSession>>();
        for (var i = 0; i < session.PlayerPerformance.Count; i++)
        {
            var existing = session.PlayerPerformance[i];
            if (string.IsNullOrEmpty(existing.Uid)) continue;
            if (reported.Contains(existing.Uid)) continue;

            var lastFps = existing.Fps.Count > 0 ? existing.Fps[^1] : 0;
            gapExtensions.Add(
                lastFps < 0
                    ? update.Set(x => x.PlayerPerformance[i].Fps[existing.Fps.Count - 1], lastFps - 5)
                    : update.Push(x => x.PlayerPerformance[i].Fps, -5)
            );
        }

        if (gapExtensions.Count > 0)
        {
            await sessionsContext.Update(session.Id, update.Combine(gapExtensions));
        }
    }

    public async Task ComputeFinalFpsStatsAsync(string sessionId)
    {
        var session = sessionsContext.FindFirst(s => s.SessionId == sessionId);
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
            var average = samples.Average();
            var min = samples[0];
            var max = samples[^1];

            var update = Builders<PlayerMissionStats>.Update.SetOnInsert(x => x.MissionSessionId, sessionId)
                                                     .SetOnInsert(x => x.PlayerUid, player.Uid)
                                                     .Set(x => x.FpsP1, p1)
                                                     .Set(x => x.FpsAverage, average)
                                                     .Min(x => x.FpsMin, min)
                                                     .Max(x => x.FpsMax, max);

            await playerStatsContext.Upsert(x => x.MissionSessionId == sessionId && x.PlayerUid == player.Uid, update);
        }
    }
}
