using System;
using System.Collections.Generic;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Treats classifier JSON as untrusted. Rejects the whole batch on any contract break.
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

            var tag = (c.Tag ?? string.Empty).Trim().ToLowerInvariant();
            if (!NpcGuardedTags.IsKnown(tag)) return null;

            int? slot = c.TopicSlot;
            if (slot is not null)
            {
                if (tag != NpcGuardedTags.RelevantQuestion) return null;
                if (slot is < 1 or > 3) return null;
            }

            var evidence = c.Evidence ?? string.Empty;
            if (!c.Ambiguous && IsActionable(tag))
            {
                if (string.IsNullOrWhiteSpace(evidence)) return null;
                if (string.IsNullOrEmpty(u.Text) || !u.Text.Contains(evidence, StringComparison.OrdinalIgnoreCase)) return null;
            }

            cleaned.Add(
                new NpcGuardedClassification
                {
                    T = c.T,
                    Tag = tag,
                    TopicSlot = slot,
                    AddressesConcern = c.AddressesConcern || tag == NpcGuardedTags.AddressesConcern,
                    Ambiguous = c.Ambiguous,
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
