using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Admin-console telemetry: one npc_debug_state per handled turn — including silent and
// cancelled turns — so the in-game inspector can see what the pipeline decided. Classifier
// free text is redacted of canonical fact text before it leaves the API.
public partial class NpcBrokerService
{
    private static string AddressDecisionWire(AddressDecision decision) =>
        decision switch
        {
            AddressDecision.StaySilent  => "stay_silent",
            AddressDecision.AskTheBrain => "ask_the_brain",
            _                           => "answer"
        };

    private Task SendDebugStateAsync(
        int apiPort,
        string npcId,
        string provider,
        string addressDecision,
        IReadOnlyList<NpcGuardedClassification> classifications = null,
        NpcGuardedConfig config = null,
        long classifyMs = 0,
        long replyMs = 0,
        string eligibleFactId = null,
        IReadOnlyList<string> disclosedFactIds = null
    )
    {
        var classes = (classifications ?? []).Where(c => c is not null).ToList();
        var tag = classes.Count == 0 ? "" : string.Join(",", classes.Select(c => c.Tag));
        var reason = SummariseDebugFreeText(classes.Select(c => c.Reason), config);
        var evidence = SummariseDebugFreeText(classes.Select(c => c.Evidence), config);

        return commandSender.SendCommandAsync(
            apiPort,
            NpcAudioEnvelopeBuilder.BuildDebugState(
                npcId,
                provider,
                addressDecision,
                tag,
                classes.LastOrDefault()?.TopicSlot,
                classes.Any(c => c.AddressesConcern),
                classes.Any(c => c.Ambiguous),
                reason,
                evidence,
                classifyMs,
                replyMs,
                eligibleFactId,
                disclosedFactIds
            )
        );
    }

    private Task SendGuardedDebugStateAsync(
        int apiPort,
        string npcId,
        NpcGuardedClassifyResult classify,
        NpcGuardedConfig config,
        NpcGuardedEngineResult engine,
        NpcGuardedReplyResult reply,
        IReadOnlyList<string> disclosedFactIds
    ) =>
        SendDebugStateAsync(
            apiPort,
            npcId,
            reply?.Provider ?? classify?.Provider,
            "answer",
            classify?.Classifications,
            config,
            classify?.Ms ?? 0,
            reply?.Ms ?? 0,
            engine?.PermittedFactId,
            disclosedFactIds
        );

    private static string SummariseDebugFreeText(IEnumerable<string> parts, NpcGuardedConfig config)
    {
        var clean = parts.Where(p => !string.IsNullOrEmpty(p)).Select(p => RedactCanonical(p, config)).Take(3);
        return string.Join("; ", clean);
    }
}
