using System.Collections.Generic;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Bounds the conversation sent to the brain.
///
/// A player can keep talking to an NPC for as long as they like, and every exchange is
/// kept, so without a ceiling the prompt grows for the whole mission and each reply gets
/// slower than the last. The stored history keeps its full depth; only the slice handed
/// to the brain is trimmed, newest first, so the NPC always has the recent thread.
public static class NpcHistoryBudget
{
    public const int MaxChars = 6000;

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
