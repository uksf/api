using MongoDB.Bson;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IMissionStatsService
{
    Task<MissionSession> FindOrCreateSessionAsync(string mission, string map, DateTime receivedAt);
    Task<MissionStatsBatch> StoreRawBatchAsync(string sessionId, string mission, string map, List<BsonDocument> events, DateTime receivedAt);
    Task UpdatePlayerStatsAsync(string sessionId, string playerUid, PlayerMissionStats updates);
    Task UpdateMissionStatsAsync(string sessionId, MissionStats updates);
}

public class MissionStatsService(
    IMissionSessionsContext sessionsContext,
    IMissionStatsBatchesContext batchesContext,
    IPlayerMissionStatsContext playerStatsContext,
    IMissionStatsContext missionStatsContext,
    IVariablesService variablesService
) : IMissionStatsService
{
    private const int DefaultSessionGapHours = 4;

    public async Task<MissionSession> FindOrCreateSessionAsync(string mission, string map, DateTime receivedAt)
    {
        var gapHours = GetSessionGapHours();
        var cutoff = receivedAt.AddHours(-gapHours);

        var existing = sessionsContext.Get(s => s.Mission == mission && s.Map == map && s.LastBatchReceived >= cutoff)
                                      .OrderByDescending(s => s.LastBatchReceived)
                                      .FirstOrDefault();

        if (existing is not null)
        {
            existing.TotalBatchesReceived++;
            await sessionsContext.Update(existing.Id, x => x.LastBatchReceived, receivedAt);
            await sessionsContext.Update(existing.Id, x => x.TotalBatchesReceived, existing.TotalBatchesReceived);
            return existing;
        }

        var session = new MissionSession
        {
            Mission = mission,
            Map = map,
            Type = GetMissionType(receivedAt.DayOfWeek),
            Date = receivedAt.Date,
            FirstBatchReceived = receivedAt,
            LastBatchReceived = receivedAt,
            TotalBatchesReceived = 1
        };

        await sessionsContext.Add(session);
        return session;
    }

    public async Task<MissionStatsBatch> StoreRawBatchAsync(string sessionId, string mission, string map, List<BsonDocument> events, DateTime receivedAt)
    {
        var batch = new MissionStatsBatch
        {
            MissionSessionId = sessionId,
            Mission = mission,
            Map = map,
            Events = events,
            ReceivedAt = receivedAt
        };

        await batchesContext.Add(batch);
        return batch;
    }

    public async Task UpdatePlayerStatsAsync(string sessionId, string playerUid, PlayerMissionStats updates)
    {
        var existing = playerStatsContext.GetSingle(s => s.MissionSessionId == sessionId && s.PlayerUid == playerUid);

        if (existing is null)
        {
            updates.MissionSessionId = sessionId;
            updates.PlayerUid = playerUid;
            await playerStatsContext.Add(updates);
            return;
        }

        existing.TotalShots += updates.TotalShots;
        existing.TotalHits += updates.TotalHits;
        existing.TotalDistance += updates.TotalDistance;
        existing.HitCount += updates.HitCount;
        MergeDictionary(existing.BodyPartHits, updates.BodyPartHits);
        MergeWeaponBreakdown(existing.WeaponBreakdown, updates.WeaponBreakdown);

        await playerStatsContext.Replace(existing);
    }

    public async Task UpdateMissionStatsAsync(string sessionId, MissionStats updates)
    {
        var existing = missionStatsContext.GetSingle(s => s.MissionSessionId == sessionId);

        if (existing is null)
        {
            updates.MissionSessionId = sessionId;
            await missionStatsContext.Add(updates);
            return;
        }

        MergeDictionary(existing.EventCounts, updates.EventCounts);
        await missionStatsContext.Replace(existing);
    }

    private int GetSessionGapHours()
    {
        try
        {
            var variable = variablesService.GetVariable("MISSION_STATS_SESSION_GAP_HOURS");
            if (int.TryParse(variable?.Item?.ToString(), out var hours))
            {
                return hours;
            }
        }
        catch
        {
            // Variable missing or inaccessible
        }

        return DefaultSessionGapHours;
    }

    private static MissionType GetMissionType(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Saturday  => MissionType.MainOp,
            DayOfWeek.Wednesday => MissionType.Training,
            _                   => MissionType.SideOp
        };

    private static void MergeDictionary(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = target.GetValueOrDefault(key) + value;
        }
    }

    private static void MergeWeaponBreakdown(Dictionary<string, WeaponStats> target, Dictionary<string, WeaponStats> source)
    {
        foreach (var (weapon, sourceStats) in source)
        {
            if (!target.TryGetValue(weapon, out var targetStats))
            {
                target[weapon] = sourceStats;
                continue;
            }

            targetStats.Shots += sourceStats.Shots;
            targetStats.Hits += sourceStats.Hits;
            MergeDictionary(targetStats.FireModes, sourceStats.FireModes);
        }
    }
}
