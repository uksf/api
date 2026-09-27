using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// The brain the broker talks to. Jev decides every turn and Gemini only writes; see
/// NpcGuardedJevBrain and NpcPlainJevBrain. NpcBrainService keeps prerendering, and serves a
/// plain turn when Jev is unreachable. A guarded turn never falls back: it fails closed.
public class NpcJevBrainClient(NpcBrainService oneCall, NpcGuardedJevBrain guarded, NpcPlainJevBrain plain, INpcJevClient jev, IUksfLogger logger)
    : INpcBrainClient
{
    public Task<PrerenderResult> PrerenderAsync(PrerenderRequest request) => oneCall.PrerenderAsync(request);

    public async Task<bool?> IsAddressedAsync(IReadOnlyList<string> names, string me, bool lookedAt, string text, string npcId)
    {
        var (state, questions) = NpcPlainJevDecider.BuildAddressFor(names, me, lookedAt, text);
        var answers = await jev.AskAsync(state, questions, npcId);
        return answers is null ? null : answers.P("to_me") >= NpcGuardedJevDecider.Yes;
    }

    public async Task<RespondResult> RespondAsync(RespondRequest request)
    {
        var turn = await plain.TurnAsync(request);
        if (turn.Decision is null)
        {
            logger.LogWarning($"NPC turn '{request.NpcId}': jev unavailable, one-call fallback");
            return await oneCall.RespondAsync(request);
        }

        var provider = $"jev{(request.Mode == "scripted" ? "" : "+writer")}";
        logger.LogInfo(
            $"NPC turn '{request.NpcId}' ({request.Mode}) via {provider}: mood={turn.Decision.Mood} known={turn.Decision.Known} noise={turn.Decision.Noise} rewritten={turn.Rewritten} jev={turn.Decision.Ms}ms write={turn.WriteMs}ms"
        );
        if (request.Mode == "scripted")
            return new RespondResult
            {
                Text = turn.Text,
                LineId = turn.LineId,
                Provider = provider,
                Mood = turn.Decision.Mood,
                Decision = turn.Decision
            };
        if (turn.Text is null) return null;

        return new RespondResult
        {
            Text = NpcReplyCleaner.Clean(turn.Text),
            Provider = provider,
            Mood = turn.Decision.Mood,
            Emote = turn.Emote,
            Decision = turn.Decision,
            VoiceId = oneCall.ResolveVoiceId(request.VoiceId, turn.Decision.Mood)
        };
    }

    public async Task<NpcGuardedTurnResult> TurnGuardedAsync(NpcGuardedTurnRequest request)
    {
        var turn = await guarded.TurnAsync(request, request.Config);
        if (turn.Classifications is null) return new NpcGuardedTurnResult { Reply = new NpcGuardedReplyResult { Ok = false, Failure = turn.Failure } };

        logger.LogInfo(
            $"NPC guarded turn '{request.NpcId}' via jev+writer: tags=[{string.Join(",", turn.Classifications.Select(c => c.Tag))}] directive={turn.Engine.Directive} jev={turn.DecideMs}ms write={turn.WriteMs}ms"
        );
        return new NpcGuardedTurnResult
        {
            Classify = new NpcGuardedClassifyResult
            {
                Classifications = turn.Classifications,
                Provider = "jev",
                Ms = turn.DecideMs
            },
            Reply = new NpcGuardedReplyResult
            {
                Ok = turn.Failure is null,
                Failure = turn.Failure,
                Text = turn.Text ?? "",
                Mood = turn.Mood,
                Emote = turn.Emote,
                // The engine, not the writer, decides disclosure; the writer only saw the permitted fact.
                DisclosedFactId = turn.Engine.Directive == NpcGuardedDirectives.Disclose ? turn.Engine.PermittedFactId : null,
                Provider = "jev+writer",
                VoiceId = oneCall.ResolveVoiceId(request.VoiceId, turn.Mood),
                Ms = turn.WriteMs
            }
        };
    }
}
