using System;
using System.Collections.Generic;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Treats classifier JSON as untrusted. Count and timestamp breaks reject the batch.
/// Unknown tags, bad slots, and missing evidence fall back in place so a spoken line can still land.
public static class NpcGuardedClassificationValidator
{
    public static List<NpcGuardedClassification> Validate(IReadOnlyList<NpcGuardedClassification> raw, IReadOnlyList<NpcTurnDto> utterances)
    {
        if (raw is null || utterances is null) return null;
        if (raw.Count != utterances.Count) return null;

        var cleaned = new List<NpcGuardedClassification>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var c = raw[i] ?? new NpcGuardedClassification();
            var u = utterances[i];
            if (c.T != u.T) return null;

            var tag = NpcGuardedTags.Normalise(c.Tag);
            var slot = tag == NpcGuardedTags.RelevantQuestion && c.TopicSlot is >= 1 and <= 3 ? c.TopicSlot : null;
            var evidence = c.Evidence ?? string.Empty;
            var ambiguous = c.Ambiguous;
            if (!ambiguous &&
                IsActionable(tag) &&
                (string.IsNullOrWhiteSpace(evidence) || string.IsNullOrEmpty(u.Text) || !u.Text.Contains(evidence, StringComparison.OrdinalIgnoreCase)))
            {
                ambiguous = true;
            }

            cleaned.Add(
                new NpcGuardedClassification
                {
                    T = c.T,
                    Tag = tag,
                    TopicSlot = slot,
                    AddressesConcern = c.AddressesConcern || tag == NpcGuardedTags.AddressesConcern,
                    Ambiguous = ambiguous,
                    Reason = c.Reason ?? string.Empty,
                    Evidence = evidence
                }
            );
        }

        return cleaned;
    }

    private static bool IsActionable(string tag) =>
        tag is NpcGuardedTags.RelevantQuestion or NpcGuardedTags.Rapport or NpcGuardedTags.Pressure or NpcGuardedTags.Threat or NpcGuardedTags.BackOff
            or NpcGuardedTags.AddressesConcern;
}
