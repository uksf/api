using System.Collections.Generic;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Bounds the conversation sent to the brain.
///
/// A player can keep talking to an NPC for as long as they like and every exchange is kept,
/// so the prompt needs a ceiling or it grows for the whole mission. Measured against the
/// live brain, reply latency is flat from 500 to 6000 tokens of prompt — length is not what
/// costs the time — so the ceiling is set for memory depth rather than speed, and only the
/// oldest exchanges fall off. The stored history keeps its full depth regardless.
public static class NpcHistoryBudget
{
    public const int MaxChars = 10000;

    public static List<NpcHistoryEntry> Trim(List<NpcHistoryEntry> history, int maxChars = MaxChars)
    {
        if (history is null || history.Count == 0) return [];

        var kept = new List<NpcHistoryEntry>();
        var used = 0;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var entry = history[i];
            var cost = (entry.Text?.Length ?? 0) + (entry.Speaker?.Length ?? 0);
            if (used + cost > maxChars && kept.Count > 0) break;

            kept.Add(entry);
            used += cost;
        }

        kept.Reverse();
        return kept;
    }
}
