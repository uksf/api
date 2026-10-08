using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Services;

public interface IMissionStatsService
{
    Task<MissionSession> GetOrCreateSessionAsync(string sessionId, string mission, string map, DateTime receivedAt);
    Task<MissionSession> GetSessionAsync(string sessionId);
    Task UpdatePlayerStatsAsync(string sessionId, string playerUid, PlayerMissionStats updates);
    Task UpdateMissionStatsAsync(string sessionId, MissionStats updates);
    Task HandleMissionStartedAsync(string sessionId, string mission, string map, DateTime timestamp);
    Task HandleMissionEndedAsync(string sessionId, double durationSeconds, DateTime timestamp);
    Task HandlePlayerConnectedAsync(string sessionId, string uid, string name, DateTime timestamp);
    Task HandlePlayerDisconnectedAsync(string sessionId, string uid, DateTime timestamp);
    Task FinaliseKilledSessionAsync(string sessionId);
}

public partial class MissionStatsService(
    IMissionSessionsContext sessionsContext,
    IRawEventStore rawEventStore,
    IPlayerMissionStatsContext playerStatsContext,
    IMissionStatsContext missionStatsContext,
    IPerformanceService performanceService,
    IUksfLogger logger
) : IMissionStatsService
{
    public async Task<MissionSession> GetOrCreateSessionAsync(string sessionId, string mission, string map, DateTime receivedAt)
    {
        var existing = sessionsContext.FindFirst(s => s.SessionId == sessionId);

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
            SessionId = sessionId,
            Mission = mission,
            Map = map,
            FirstBatchReceived = receivedAt,
            LastBatchReceived = receivedAt,
            TotalBatchesReceived = 1
        };

        await sessionsContext.Add(session);
        return session;
    }

    public Task<MissionSession> GetSessionAsync(string sessionId)
    {
        return Task.FromResult(sessionsContext.FindFirst(s => s.SessionId == sessionId));
    }

    public async Task UpdatePlayerStatsAsync(string sessionId, string playerUid, PlayerMissionStats updates)
    {
        var updateBuilder = Builders<PlayerMissionStats>.Update.SetOnInsert(x => x.MissionSessionId, sessionId)
                                                        .SetOnInsert(x => x.PlayerUid, playerUid)
                                                        .Inc(x => x.TotalShots, updates.TotalShots)
                                                        .Inc(x => x.TotalHits, updates.TotalHits)
                                                        .Inc(x => x.BallisticShots, updates.BallisticShots)
                                                        .Inc(x => x.BallisticHits, updates.BallisticHits)
                                                        .Inc(x => x.ExplosiveShots, updates.ExplosiveShots)
                                                        .Inc(x => x.ExplosiveHits, updates.ExplosiveHits)
                                                        .Inc(x => x.OtherShots, updates.OtherShots)
                                                        .Inc(x => x.OtherHits, updates.OtherHits)
                                                        .Inc(x => x.Kills.Direct, updates.Kills.Direct)
                                                        .Inc(x => x.Kills.Indirect, updates.Kills.Indirect)
                                                        .Inc(x => x.TotalDamageDealt, updates.TotalDamageDealt)
                                                        .Inc(x => x.TimesWounded, updates.TimesWounded)
                                                        .Inc(x => x.DistanceOnFoot, updates.DistanceOnFoot)
                                                        .Inc(x => x.DistanceInVehicle, updates.DistanceInVehicle)
                                                        .Inc(x => x.TotalFuelLitres, updates.TotalFuelLitres)
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

        foreach (var (targetType, typeStats) in updates.KillsByTargetType)
        {
            updateBuilder = updateBuilder.Inc(x => x.KillsByTargetType[targetType].Count, typeStats.Count);

            foreach (var (classname, count) in typeStats.Types)
            {
                updateBuilder = updateBuilder.Inc(x => x.KillsByTargetType[targetType].Types[classname], count);
            }
        }

        foreach (var (weapon, weaponStats) in updates.KillsByWeapon)
        {
            updateBuilder = updateBuilder.Inc(x => x.KillsByWeapon[weapon].Count, weaponStats.Count);

            foreach (var (ammoType, count) in weaponStats.Ammo)
            {
                updateBuilder = updateBuilder.Inc(x => x.KillsByWeapon[weapon].Ammo[ammoType], count);
            }
        }

        foreach (var (part, count) in updates.WoundsByBodyPart)
        {
            updateBuilder = updateBuilder.Inc(x => x.WoundsByBodyPart[part], count);
        }

        foreach (var (damageType, count) in updates.WoundsByDamageType)
        {
            updateBuilder = updateBuilder.Inc(x => x.WoundsByDamageType[damageType], count);
        }

        foreach (var (ammoType, damage) in updates.DamageDealtByAmmo)
        {
            updateBuilder = updateBuilder.Inc(x => x.DamageDealtByAmmo[ammoType], damage);
        }

        foreach (var (weapon, sourceStats) in updates.WeaponBreakdown)
        {
            updateBuilder = updateBuilder.Inc(x => x.WeaponBreakdown[weapon].Shots, sourceStats.Shots)
                                         .Inc(x => x.WeaponBreakdown[weapon].Hits, sourceStats.Hits)
                                         .Inc(x => x.WeaponBreakdown[weapon].EngagementDistanceSum, sourceStats.EngagementDistanceSum);

            if (sourceStats.MinEngagementDistance < double.MaxValue)
            {
                updateBuilder = updateBuilder.Min(x => x.WeaponBreakdown[weapon].MinEngagementDistance, sourceStats.MinEngagementDistance);
            }

            if (sourceStats.MaxEngagementDistance > 0)
            {
                updateBuilder = updateBuilder.Max(x => x.WeaponBreakdown[weapon].MaxEngagementDistance, sourceStats.MaxEngagementDistance);
            }

            foreach (var (ammoType, ammoStats) in sourceStats.AmmoBreakdown)
            {
                updateBuilder = updateBuilder.Inc(x => x.WeaponBreakdown[weapon].AmmoBreakdown[ammoType].Shots, ammoStats.Shots)
                                             .Inc(x => x.WeaponBreakdown[weapon].AmmoBreakdown[ammoType].Hits, ammoStats.Hits)
                                             .Inc(
                                                 x => x.WeaponBreakdown[weapon].AmmoBreakdown[ammoType].EngagementDistanceSum,
                                                 ammoStats.EngagementDistanceSum
                                             );

                if (ammoStats.MinEngagementDistance < double.MaxValue)
                {
                    updateBuilder = updateBuilder.Min(
                        x => x.WeaponBreakdown[weapon].AmmoBreakdown[ammoType].MinEngagementDistance,
                        ammoStats.MinEngagementDistance
                    );
                }

                if (ammoStats.MaxEngagementDistance > 0)
                {
                    updateBuilder = updateBuilder.Max(
                        x => x.WeaponBreakdown[weapon].AmmoBreakdown[ammoType].MaxEngagementDistance,
                        ammoStats.MaxEngagementDistance
                    );
                }

                foreach (var (bodyPart, count) in ammoStats.BodyPartHits)
                {
                    updateBuilder = updateBuilder.Inc(x => x.WeaponBreakdown[weapon].AmmoBreakdown[ammoType].BodyPartHits[bodyPart], count);
                }
            }
        }

        await playerStatsContext.Upsert(x => x.MissionSessionId == sessionId && x.PlayerUid == playerUid, updateBuilder);
    }

    public async Task UpdateMissionStatsAsync(string sessionId, MissionStats updates)
    {
        if (updates.VehiclesDestroyed <= 0)
        {
            return;
        }

        var update = Builders<MissionStats>.Update.SetOnInsert(x => x.MissionSessionId, sessionId).Inc(x => x.VehiclesDestroyed, updates.VehiclesDestroyed);

        await missionStatsContext.Upsert(x => x.MissionSessionId == sessionId, update);
    }

    public async Task HandleMissionStartedAsync(string sessionId, string mission, string map, DateTime timestamp)
    {
        var session = await GetOrCreateSessionAsync(sessionId, mission, map, timestamp);
        var update = Builders<MissionSession>.Update.Set(x => x.MissionStarted, timestamp);
        await sessionsContext.Update(session.Id, update);
    }

    public async Task HandleMissionEndedAsync(string sessionId, double durationSeconds, DateTime timestamp)
    {
        var existing = sessionsContext.FindFirst(s => s.SessionId == sessionId);
        if (existing is null)
        {
            return;
        }

        var update = Builders<MissionSession>.Update.Set(x => x.MissionEnded, timestamp).Set(x => x.DurationSeconds, durationSeconds);
        await sessionsContext.Update(existing.Id, update);

        await performanceService.ComputeFinalFpsStatsAsync(sessionId);
    }
}
