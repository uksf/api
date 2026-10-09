using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public partial class MissionStatsService
{
    public async Task FinaliseKilledSessionAsync(string sessionId)
    {
        var session = sessionsContext.FindFirst(s => s.SessionId == sessionId);
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

        if (!await sessionsContext.FindAndUpdate(s => s.SessionId == sessionId && s.MissionEnded == null, Builders<MissionSession>.Update.Combine(updates)))
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
        var existing = sessionsContext.FindFirst(s => s.SessionId == sessionId);
        if (existing is null)
        {
            return;
        }

        await CloseOpenPresenceAsync(existing, uid, timestamp);

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
        var existing = sessionsContext.FindFirst(s => s.SessionId == sessionId);
        if (existing is null)
        {
            return;
        }

        await CloseOpenPresenceAsync(existing, uid, timestamp);
    }

    private async Task CloseOpenPresenceAsync(MissionSession session, string uid, DateTime timestamp)
    {
        var openIndex = session.PlayerPresence.FindLastIndex(p => p.Uid == uid && p.Disconnected is null);
        if (openIndex < 0)
        {
            return;
        }

        await sessionsContext.Update(session.Id, Builders<MissionSession>.Update.Set(x => x.PlayerPresence[openIndex].Disconnected, timestamp));
    }
}
