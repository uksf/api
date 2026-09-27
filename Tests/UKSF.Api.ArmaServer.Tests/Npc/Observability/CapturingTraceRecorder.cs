using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Observability;

namespace UKSF.Api.ArmaServer.Tests.Npc.Observability;

public sealed record CapturedTrace(string Type, string Session, string Npc, string Turn, string Utt, BsonDocument Data);

/// Keeps recorded events in memory, converted the same way the real recorder converts them.
public sealed class CapturingTraceRecorder : INpcTraceRecorder
{
    public List<CapturedTrace> Events { get; } = [];

    public void Record(string type, string session, object data, string npc = null, string turn = null, string utt = null)
    {
        var body = data as BsonDocument ?? (data is null ? null : BsonDocument.Parse(JsonSerializer.Serialize(data, NpcBrainJson.Options)));
        lock (Events) Events.Add(new CapturedTrace(type, session, npc, turn, utt, body));
    }

    public CapturedTrace Single(string type) => Events.Single(e => e.Type == type);
}
