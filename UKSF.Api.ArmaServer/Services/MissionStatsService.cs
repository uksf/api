using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Services;

public interface IMissionStatsService
{
    Task<MissionSession> GetOrCreateSessionAsync(string sessionId, string mission, string map, DateTime receivedAt);
    Task<MissionStatsBatch> StoreRawBatchAsync(string sessionId, string mission, string map, List<BsonDocument> events, DateTime receivedAt);
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
    IMissionStatsBatchesContext batchesContext,
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

        await MergeBatchesAsync(sessionId);
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

        var endTimestamp = session.LastBatchReceived != default ? session.LastBatchReceived : session.MissionStarted ?? DateTime.UtcNow;

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

        await MergeBatchesAsync(sessionId);
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

        await StoreRawBatchAsync(session.SessionId, session.Mission, session.Map, syntheticEvents, endTimestamp);

        var eventCounts = new Dictionary<string, int> { ["mission_ended"] = 1 };
        if (closedEntries.Count > 0)
        {
            eventCounts["player_disconnected"] = closedEntries.Count;
        }

        await UpdateMissionStatsAsync(session.SessionId, new MissionStats { EventCounts = eventCounts });
    }

    private async Task MergeBatchesAsync(string sessionId)
    {
        var batches = batchesContext.Get(b => b.MissionSessionId == sessionId).OrderBy(b => b.ReceivedAt).ToList();
        if (batches.Count <= 1)
        {
            return;
        }

        var mergedBatch = new MissionStatsBatch
        {
            MissionSessionId = sessionId,
            Mission = batches[0].Mission,
            Map = batches[0].Map,
            ReceivedAt = batches[0].ReceivedAt,
            Events = batches.SelectMany(b => b.Events).ToList()
        };

        var originalIds = batches.Select(b => b.Id).ToHashSet();
        await batchesContext.Add(mergedBatch);
        await batchesContext.DeleteMany(b => originalIds.Contains(b.Id));
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
