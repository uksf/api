using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Pure validator/composer for guarded reply model output. No I/O, no retry.
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
        if (output is null) return Fail("null output");

        var text = (output.Text ?? string.Empty).Trim();
        if (text.Length == 0) return Fail("empty text");

        var mood = string.IsNullOrWhiteSpace(output.Mood) ? MoodScripts.Neutral : output.Mood.Trim().ToLowerInvariant();
        if (!MoodScripts.IsValid(mood)) return Fail($"invalid mood '{output.Mood}'");

        var canonicalFacts = (config?.Facts ?? []).Where(f => !string.IsNullOrEmpty(f.Text)).ToList();
        var emote = string.IsNullOrWhiteSpace(output.Emote) ? null : output.Emote.Trim();
        if (emote is not null)
        {
            if (emote.Length > MaxEmoteLength) return Fail("emote too long");
            if (emote.Contains('\n') || emote.Contains('\r')) return Fail("emote unsafe");
            if (canonicalFacts.Any(f => ContainsIgnoreCase(emote, f.Text))) return Fail("canonical fact text in emote");
        }

        var claimedId = string.IsNullOrWhiteSpace(output.DisclosedFactId) ? null : output.DisclosedFactId.Trim();
        if (claimedId is not null && !string.Equals(claimedId, permittedFactId, StringComparison.Ordinal)) return Fail("unauthorised fact id");

        var alreadyPublic = new HashSet<string>(disclosedFactIds ?? [], StringComparer.Ordinal);
        foreach (var fact in canonicalFacts)
        {
            if (alreadyPublic.Contains(fact.Id)) continue;
            if (!ContainsIgnoreCase(text, fact.Text)) continue;
            // Undisclosed canonical text is engine-owned; model must not author it.
            return Fail("canonical fact text in model output");
        }

        var spoken = text;
        string disclosedId = null;
        if (!string.IsNullOrEmpty(permittedFactId) && string.Equals(claimedId, permittedFactId, StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(permittedFactText)) return Fail("missing canonical fact text");
            spoken = AppendCanonical(text, permittedFactText);
            disclosedId = permittedFactId;
        }

        return new NpcGuardedValidatedReply
        {
            Ok = true,
            SpokenText = spoken,
            Mood = mood,
            Emote = emote,
            DisclosedFactId = disclosedId
        };
    }

    public static string AppendCanonical(string dialogue, string canonical)
    {
        var d = dialogue.Trim();
        var c = canonical.Trim();
        if (d.Length == 0) return c;
        if (ContainsIgnoreCase(d, c)) return d;
        var needsSpace = !char.IsWhiteSpace(d[^1]);
        var joiner = needsSpace ? " " : "";
        return d + joiner + c;
    }

    private static bool ContainsIgnoreCase(string haystack, string needle) => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static NpcGuardedValidatedReply Fail(string reason) => new() { Ok = false, Failure = reason };
}
