using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Shape-check for guarded reply JSON. Does not rewrite speech.
public static class NpcGuardedReplyValidator
{
    public const int MaxEmoteLength = 40;

    public static NpcGuardedValidatedReply Validate(
        NpcGuardedReplyModelOutput output,
        NpcGuardedConfig config,
        string permittedFactId,
        string permittedFactText,
        IReadOnlyCollection<string> disclosedFactIds = null
    )
    {
        NpcGuardedFact included = null;
        if (!string.IsNullOrEmpty(permittedFactId))
        {
            included = new NpcGuardedFact { Id = permittedFactId, Text = permittedFactText ?? "" };
        }

        return Validate(output, included);
    }

    public static NpcGuardedValidatedReply Validate(NpcGuardedReplyModelOutput output, NpcGuardedFact includedNext)
    {
        if (output is null) return Fail("null output");

        var text = (output.Text ?? string.Empty).Trim();
        if (text.Length == 0) return Fail("empty text");

        var emote = string.IsNullOrWhiteSpace(output.Emote) ? null : output.Emote.Trim();
        if (emote is not null)
        {
            if (emote.Length > MaxEmoteLength) return Fail("emote too long");
            if (emote.Contains('\n') || emote.Contains('\r')) return Fail("emote unsafe");
        }

        return new NpcGuardedValidatedReply
        {
            Ok = true,
            SpokenText = text,
            Mood = MoodScripts.Normalise(output.Mood),
            Emote = emote,
            DisclosedFactId = ToldIncludedNext(output.DisclosedFactId, text, includedNext)
        };
    }

    public static string ToldIncludedNext(string claimedId, string spoken, NpcGuardedFact includedNext)
    {
        if (includedNext is null || string.IsNullOrEmpty(includedNext.Id)) return null;
        if (!string.IsNullOrWhiteSpace(claimedId) && NpcGuardedFactIds.Same(claimedId, includedNext.Id)) return includedNext.Id;
        if (!string.IsNullOrEmpty(includedNext.Text) &&
            !string.IsNullOrEmpty(spoken) &&
            spoken.Contains(includedNext.Text, StringComparison.OrdinalIgnoreCase))
        {
            return includedNext.Id;
        }

        return null;
    }

    /// The rules engine, not the model, decides disclosure. The prompt carries only the next fact's
    /// text, so a reply that claims that fact on a turn the engine did not unlock has probably told
    /// it: reject. A stray claim of any other fact cannot carry words the model never saw, so
    /// Validate simply drops it. Quoting any withheld fact in speech or emote is always rejected. A
    /// paraphrase without a claim cannot be caught here; the prompt is the only guard for that.
    /// `claimedFactId` is the model's raw claim, before Validate drops it.
    public static NpcGuardedValidatedReply Guard(
        NpcGuardedValidatedReply reply,
        string claimedFactId,
        string includedNextId,
        NpcGuardedConfig config,
        IReadOnlyCollection<string> disclosedFactIds,
        string permittedFactId
    )
    {
        if (reply is not { Ok: true }) return reply;

        bool Permitted(string id) => !string.IsNullOrEmpty(permittedFactId) && NpcGuardedFactIds.Same(id, permittedFactId);
        bool Disclosed(string id) => (disclosedFactIds ?? []).Any(d => NpcGuardedFactIds.Same(d, id));

        bool IsNext(string id) => !string.IsNullOrEmpty(includedNextId) && NpcGuardedFactIds.Same(id, includedNextId);
        foreach (var claim in new[] { claimedFactId, reply.DisclosedFactId })
        {
            if (!string.IsNullOrWhiteSpace(claim) && IsNext(claim) && !Permitted(claim)) return Fail("disclosure not permitted");
        }

        foreach (var fact in config?.Facts ?? [])
        {
            if (string.IsNullOrEmpty(fact.Text) || Permitted(fact.Id) || Disclosed(fact.Id)) continue;
            if (Quotes(reply.SpokenText, fact.Text) || Quotes(reply.Emote, fact.Text)) return Fail("withheld fact text in reply");
        }

        return reply;
    }

    private static bool Quotes(string said, string fact) =>
        !string.IsNullOrEmpty(said) && said.Contains(fact.Trim().TrimEnd('.', '!', '?'), StringComparison.OrdinalIgnoreCase);

    private static NpcGuardedValidatedReply Fail(string reason) => new() { Ok = false, Failure = reason };
}
