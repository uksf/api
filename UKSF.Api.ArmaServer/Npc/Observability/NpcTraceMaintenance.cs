using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Observability;

/// Closes traces whose mission never sent mission_ended, and removes player identity from traces
/// older than 180 days. Runs off the player path on its own timer.
public sealed class NpcTraceMaintenance(IMongoDatabase database, IGameServersContext gameServers, INpcTraceRecorder trace, IUksfLogger logger)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan AbandonAfter = TimeSpan.FromHours(12);
    public static readonly TimeSpan AnonymiseAfter = TimeSpan.FromDays(180);
    private const int AnonymiseBatch = 20;

    private IMongoCollection<BsonDocument> Events => database.GetCollection<BsonDocument>(MongoNpcTraceSink.Collection);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextAnonymise = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CloseAbandonedAsync(DateTime.UtcNow, stoppingToken);
                if (DateTime.UtcNow >= nextAnonymise)
                {
                    await AnonymiseAsync(DateTime.UtcNow, stoppingToken);
                    nextAnonymise = DateTime.UtcNow.AddDays(1);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError("npc trace maintenance failed", exception);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) { }
        }
    }

    /// A mission is abandoned when its server no longer runs it and it has been quiet for
    /// StaleAfter, or when it has been quiet for AbandonAfter whatever the server says.
    internal async Task CloseAbandonedAsync(DateTime now, CancellationToken cancellationToken)
    {
        var lifecycle = await Events
                              .Find(
                                  Builders<BsonDocument>.Filter.In("type", new[] { "mission.started", "mission.ended" }) &
                                  Builders<BsonDocument>.Filter.Gte("at", now.AddDays(-30))
                              )
                              .Project(Builders<BsonDocument>.Projection.Include("type").Include("session"))
                              .ToListAsync(cancellationToken);
        var open = lifecycle.GroupBy(x => x["session"].AsString).Where(g => g.All(x => x["type"] != "mission.ended")).Select(g => g.Key);
        var live = gameServers.Get().Select(x => x.Status?.CurrentMissionSessionId).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();

        foreach (var session in open)
        {
            var last = await Events.Find(Builders<BsonDocument>.Filter.Eq("session", session))
                                   .SortByDescending(x => x["at"])
                                   .Limit(1)
                                   .FirstOrDefaultAsync(cancellationToken);
            if (!IsAbandoned(now - last["at"].ToUniversalTime(), live.Contains(session))) continue;
            trace.Record("mission.ended", session, new { reason = "abandoned", lastEventAt = last["at"].ToUniversalTime() });
        }
    }

    internal static bool IsAbandoned(TimeSpan quiet, bool serverRunsIt) => quiet >= (serverRunsIt ? AbandonAfter : StaleAfter);

    /// Replaces each player's UID and name with a stable per-mission pseudonym in every string of
    /// every event of the mission. Exact roster names only; STT misspellings are not chased.
    internal async Task AnonymiseAsync(DateTime now, CancellationToken cancellationToken)
    {
        var due = await Events
                        .Find(
                            Builders<BsonDocument>.Filter.Eq("type", "mission.started") &
                            Builders<BsonDocument>.Filter.Lt("at", now - AnonymiseAfter) &
                            Builders<BsonDocument>.Filter.Exists("anon", false)
                        )
                        .Limit(AnonymiseBatch)
                        .ToListAsync(cancellationToken);
        foreach (var session in due.Select(x => x["session"].AsString).Distinct())
        {
            var events = await Events.Find(Builders<BsonDocument>.Filter.Eq("session", session)).ToListAsync(cancellationToken);
            var replacements = Pseudonyms(events);
            var writes = events.Select(e =>
                                   {
                                       var clean = (BsonDocument)Scrub(e, replacements);
                                       clean["anon"] = true;
                                       return new ReplaceOneModel<BsonDocument>(Builders<BsonDocument>.Filter.Eq("_id", e["_id"]), clean);
                                   }
                               )
                               .ToList();
            if (writes.Count > 0) await Events.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
            logger.LogInfo($"npc trace: anonymised {writes.Count} events for session '{session}'");
        }
    }

    internal static List<(Regex Pattern, string Replacement)> Pseudonyms(IEnumerable<BsonDocument> events)
    {
        var players = events.Where(e => e["type"] == "utterance.received" && e.Contains("data"))
                            .Select(e => e["data"].AsBsonDocument)
                            .Where(d => d.Contains("uid"))
                            .Select(d => (Uid: d["uid"].ToString(), Name: d.GetValue("name", "").ToString()))
                            .DistinctBy(p => p.Uid)
                            .ToList();
        var replacements = new List<(Regex, string)>();
        for (var i = 0; i < players.Count; i++)
        {
            replacements.Add((new Regex(Regex.Escape(players[i].Uid)), $"player-{i + 1}"));
            if (players[i].Name.Length > 1)
            {
                replacements.Add((new Regex($@"\b{Regex.Escape(players[i].Name)}\b", RegexOptions.IgnoreCase), $"Player {i + 1}"));
            }
        }

        return replacements;
    }

    internal static BsonValue Scrub(BsonValue value, List<(Regex Pattern, string Replacement)> replacements) =>
        value switch
        {
            BsonString s   => new BsonString(replacements.Aggregate(s.Value, (text, r) => r.Pattern.Replace(text, r.Replacement))),
            BsonDocument d => new BsonDocument(d.Select(x => new BsonElement(x.Name, x.Name == "_id" ? x.Value : Scrub(x.Value, replacements)))),
            BsonArray a    => new BsonArray(a.Select(x => Scrub(x, replacements))),
            _              => value
        };
}
