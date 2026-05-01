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

public class MissionStatsService(
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
        var existing = sessionsContext.GetSingle(s => s.SessionId == sessionId);

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
        return Task.FromResult(sessionsContext.GetSingle(s => s.SessionId == sessionId));
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
        var existing = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (existing is null)
        {
            return;
        }

        var update = Builders<MissionSession>.Update.Set(x => x.MissionEnded, timestamp).Set(x => x.DurationSeconds, durationSeconds);
        await sessionsContext.Update(existing.Id, update);

        await performanceService.ComputeFinalFpsStatsAsync(sessionId);
    }

    public async Task FinaliseKilledSessionAsync(string sessionId)
    {
        var session = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (session is null)
        {
            logger.LogInfo($"FinaliseKilledSession: session '{sessionId}' not found, skipping");
            return;
        }

        if (session.MissionEnded.HasValue)
        {
            logger.LogInfo($"FinaliseKilledSession: session '{sessionId}' already ended, skipping");
            return;
        }

        // Truncate to millisecond precision to match MongoDB's BSON DateTime resolution.
        // Without this, the `DateTime.UtcNow` fallback has sub-ms ticks that are lost on write,
        // causing the re-read equality check below to incorrectly bail out.
        var rawEndTimestamp = session.LastBatchReceived != default ? session.LastBatchReceived : session.MissionStarted ?? DateTime.UtcNow;
        var endTimestamp = new DateTime(rawEndTimestamp.Ticks - rawEndTimestamp.Ticks % TimeSpan.TicksPerMillisecond, rawEndTimestamp.Kind);

        double? durationSeconds = session.MissionStarted.HasValue ? (endTimestamp - session.MissionStarted.Value).TotalSeconds : null;

        var openPresenceEntries = session.PlayerPresence.Select((p, i) => (Entry: p, Index: i)).Where(x => x.Entry.Disconnected is null).ToList();

        var updates = new List<UpdateDefinition<MissionSession>> { Builders<MissionSession>.Update.Set(x => x.MissionEnded, endTimestamp) };

        if (durationSeconds.HasValue)
        {
            updates.Add(Builders<MissionSession>.Update.Set(x => x.DurationSeconds, durationSeconds));
        }

        foreach (var (_, index) in openPresenceEntries)
        {
            updates.Add(Builders<MissionSession>.Update.Set(x => x.PlayerPresence[index].Disconnected, endTimestamp));
        }

        // Atomic claim: only update if MissionEnded is still null (prevents duplicate finalisation from concurrent paths)
        await sessionsContext.FindAndUpdate(s => s.SessionId == sessionId && s.MissionEnded == null, Builders<MissionSession>.Update.Combine(updates));

        // Re-read to verify we won the claim — if another caller set MissionEnded first, our FindAndUpdate was a no-op
        var updated = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (updated?.MissionEnded != endTimestamp)
        {
            logger.LogInfo($"FinaliseKilledSession: session '{sessionId}' was claimed by another path, skipping");
            return;
        }

        await BackfillSyntheticEventsAsync(session, openPresenceEntries.Select(x => x.Entry).ToList(), endTimestamp);

        await performanceService.ComputeFinalFpsStatsAsync(sessionId);

        logger.LogInfo($"FinaliseKilledSession: finalised session '{sessionId}' — closed {openPresenceEntries.Count} open player entries");
    }

    private async Task BackfillSyntheticEventsAsync(MissionSession session, List<PlayerPresence> closedEntries, DateTime endTimestamp)
    {
        var syntheticEvents = new List<BsonDocument>
        {
            new()
            {
                { "type", "mission_ended" },
                { "sessionId", session.SessionId },
                { "timestamp", endTimestamp.ToString("O") },
                { "synthetic", true }
            }
        };

        foreach (var entry in closedEntries)
        {
            syntheticEvents.Add(
                new BsonDocument
                {
                    { "type", "player_disconnected" },
                    { "sessionId", session.SessionId },
                    { "uid", entry.Uid },
                    { "name", entry.Name },
                    { "timestamp", endTimestamp.ToString("O") },
                    { "synthetic", true }
                }
            );
        }

        await rawEventStore.StoreAsync(session.SessionId, syntheticEvents);
    }

    public async Task HandlePlayerConnectedAsync(string sessionId, string uid, string name, DateTime timestamp)
    {
        var existing = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (existing is null)
        {
            return;
        }

        // Close any open presence entries for this player (crash recovery)
        var openIndex = existing.PlayerPresence.FindLastIndex(p => p.Uid == uid && p.Disconnected is null);
        if (openIndex >= 0)
        {
            var closeUpdate = Builders<MissionSession>.Update.Set(x => x.PlayerPresence[openIndex].Disconnected, timestamp);
            await sessionsContext.Update(existing.Id, closeUpdate);
        }

        var presence = new PlayerPresence
        {
            Uid = uid,
            Name = name,
            Connected = timestamp
        };
        var pushUpdate = Builders<MissionSession>.Update.Push(x => x.PlayerPresence, presence);
        await sessionsContext.Update(existing.Id, pushUpdate);
    }

    public async Task HandlePlayerDisconnectedAsync(string sessionId, string uid, DateTime timestamp)
    {
        var existing = sessionsContext.GetSingle(s => s.SessionId == sessionId);
        if (existing is null)
        {
            return;
        }

        var openIndex = existing.PlayerPresence.FindLastIndex(p => p.Uid == uid && p.Disconnected is null);
        if (openIndex < 0)
        {
            return;
        }

        var update = Builders<MissionSession>.Update.Set(x => x.PlayerPresence[openIndex].Disconnected, timestamp);
        await sessionsContext.Update(existing.Id, update);
    }
}
