using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Observability;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Observability half of the broker. Records go to NpcTraceRecorder, which never blocks a turn.
public partial class NpcBrokerService
{
    private readonly INpcTraceRecorder _trace = trace ?? NullNpcTraceRecorder.Instance;
    private readonly INpcTraceMissions _traceMissions = traceMissions ?? new NpcTraceMissions(NullNpcTraceRecorder.Instance);

    /// npc_utterance and npc_ack come straight from Arma and are recorded as sent.
    public Task HandleTraceEventAsync(string type, Dictionary<string, object> data)
    {
        if (!variablesService.GetFeatureState("NPC_BROKER")) return Task.CompletedTask;

        var sessionId = ToSafeString(data.GetValueOrDefault("sessionId"));
        var body = BsonDocument.Parse(JsonSerializer.Serialize(data.Where(x => x.Key is not ("sessionId" or "utt" or "npc" or "turn")).ToDictionary()));
        _traceMissions.EnsureStarted(sessionId);
        switch (type)
        {
            case "npc_utterance": _trace.Record("utterance.received", sessionId, body, utt: ToSafeString(data.GetValueOrDefault("utt"))); break;
            case "npc_ack":
                _trace.Record("turn.acked", sessionId, body, ToSafeString(data.GetValueOrDefault("npc")), ToSafeString(data.GetValueOrDefault("turn"))); break;
        }

        return Task.CompletedTask;
    }

    private void TraceRegistered(string sessionId, string npcId, DomainNpcSession session)
    {
        _traceMissions.EnsureStarted(sessionId);
        _trace.Record(
            "npc.registered",
            sessionId,
            new
            {
                profile = session.InteractionProfile,
                mode = session.Mode,
                persona = session.Persona,
                knowledge = session.Knowledge,
                voice = session.VoiceId,
                scripted = session.Mode == "scripted" ? session.Scripted : null,
                guarded = session.Guarded
            },
            npcId
        );
    }

    private void TraceMissionEnded(string sessionId, List<DomainNpcSession> sessions) =>
        _traceMissions.Ended(
            sessionId,
            sessions.Count > 0,
            new
            {
                reason = "clean",
                npcs = sessions.Select(s => new
                                   {
                                       npc = s.NpcId,
                                       name = s.Persona?.Name,
                                       history = s.History?.Count ?? 0,
                                       guardedState = s.GuardedState
                                   }
                               )
                               .ToList()
            }
        );

    private static List<string> UtteranceIds(List<object> rawTurns) =>
        rawTurns.Select(x => ToSafeString(ToDict(x).GetValueOrDefault("utt"))).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
}
