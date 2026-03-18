using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
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
            var update = Builders<MissionSession>.Update.Set(x => x.LastBatchReceived, receivedAt).Inc(x => x.TotalBatchesReceived, 1);
            await sessionsContext.Update(existing.Id, update);
            existing.LastBatchReceived = receivedAt;
            existing.TotalBatchesReceived++;
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

        var updateBuilder = Builders<PlayerMissionStats>.Update.Inc(x => x.TotalShots, updates.TotalShots)
                                                        .Inc(x => x.TotalHits, updates.TotalHits)
                                                        .Inc(x => x.Kills.Direct, updates.Kills.Direct)
                                                        .Inc(x => x.Kills.Indirect, updates.Kills.Indirect)
                                                        .Inc(x => x.Kills.Assists, updates.Kills.Assists)
                                                        .Inc(x => x.Kills.TotalAssistDamage, updates.Kills.TotalAssistDamage)
                                                        .Inc(x => x.TotalDamageDealt, updates.TotalDamageDealt)
                                                        .Inc(x => x.TimesWounded, updates.TimesWounded)
                                                        .Inc(x => x.DistanceOnFoot, updates.DistanceOnFoot)
                                                        .Inc(x => x.DistanceInVehicle, updates.DistanceInVehicle)
                                                        .Inc(x => x.TotalFuelConsumed, updates.TotalFuelConsumed)
                                                        .Inc(x => x.ExplosivesPlaced, updates.ExplosivesPlaced)
                                                        .Inc(x => x.TimesUnconscious, updates.TimesUnconscious);

        foreach (var (bodyPart, count) in updates.BodyPartHits)
        {
            updateBuilder = updateBuilder.Inc(x => x.BodyPartHits[bodyPart], count);
        }

        foreach (var (targetType, count) in updates.HitsByTargetType)
        {
            updateBuilder = updateBuilder.Inc(x => x.HitsByTargetType[targetType], count);
        }

        foreach (var (targetType, count) in updates.KillsByTargetType)
        {
            updateBuilder = updateBuilder.Inc(x => x.KillsByTargetType[targetType], count);
        }

        foreach (var (part, count) in updates.WoundsByBodyPart)
        {
            updateBuilder = updateBuilder.Inc(x => x.WoundsByBodyPart[part], count);
        }

        foreach (var (damageType, count) in updates.WoundsByDamageType)
        {
            updateBuilder = updateBuilder.Inc(x => x.WoundsByDamageType[damageType], count);
        }

        foreach (var (weapon, sourceStats) in updates.WeaponBreakdown)
        {
            updateBuilder = updateBuilder.Inc(x => x.WeaponBreakdown[weapon].Shots, sourceStats.Shots)
                                         .Inc(x => x.WeaponBreakdown[weapon].Hits, sourceStats.Hits)
                                         .Inc(x => x.WeaponBreakdown[weapon].HitCount, sourceStats.HitCount)
                                         .Inc(x => x.WeaponBreakdown[weapon].TotalEngagementDistance2D, sourceStats.TotalEngagementDistance2D)
                                         .Inc(x => x.WeaponBreakdown[weapon].TotalEngagementDistance3D, sourceStats.TotalEngagementDistance3D);

            if (sourceStats.MaxEngagementDistance2D > 0)
            {
                updateBuilder = updateBuilder.Max(x => x.WeaponBreakdown[weapon].MaxEngagementDistance2D, sourceStats.MaxEngagementDistance2D);
            }

            foreach (var (fireMode, count) in sourceStats.FireModes)
            {
                updateBuilder = updateBuilder.Inc(x => x.WeaponBreakdown[weapon].FireModes[fireMode], count);
            }
        }

        await playerStatsContext.Update(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid, updateBuilder);
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

        var updateDefinitions = updates.EventCounts.Select(kvp => Builders<MissionStats>.Update.Inc(x => x.EventCounts[kvp.Key], kvp.Value)).ToList();

        await missionStatsContext.Update(x => x.MissionSessionId == sessionId, Builders<MissionStats>.Update.Combine(updateDefinitions));
    }

    private int GetSessionGapHours()
    {
        return variablesService.GetVariable("MISSION_STATS_SESSION_GAP_HOURS").AsIntWithDefault(DefaultSessionGapHours);
    }

    private static MissionType GetMissionType(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Saturday  => MissionType.MainOp,
            DayOfWeek.Wednesday => MissionType.Training,
            _                   => MissionType.SideOp
        };
}
