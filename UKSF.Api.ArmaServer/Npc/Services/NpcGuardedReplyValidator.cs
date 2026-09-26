using System;
using System.Collections.Generic;
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

    private static NpcGuardedValidatedReply Fail(string reason) => new() { Ok = false, Failure = reason };
}
