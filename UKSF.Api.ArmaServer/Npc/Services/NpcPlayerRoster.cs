using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// What an NPC may call a player, per mission session.
///
/// An NPC has no way to know a stranger's name, so every player starts with a label —
/// "Soldier 1", "Soldier 2" — assigned in the order they first speak, stable for the
/// session and shared by every NPC in it. When a player actually introduces themselves,
/// the label upgrades to the name they gave and the whole transcript is rewritten, so a
/// speaker never appears as two different people in one history. That split is what made
/// a follow-up like "do you know?" unanswerable: the model could not tell the UID in the
/// overheard lines and the name in the addressed lines were one person.
public static class NpcPlayerRoster
{
    private record Entry(string Label, string Name);

    private static readonly ConcurrentDictionary<string, Dictionary<string, Entry>> Rosters = new();

    private static readonly Regex Introduction = new(
        @"\b(?:i'?m|i am|my name'?s|name is|call me|they call me)\s+([A-Za-z][A-Za-z'\-]{1,19})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly HashSet<string> NotNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "looking",
        "not",
        "sorry",
        "here",
        "just",
        "so",
        "fine",
        "ok",
        "okay",
        "good",
        "sure",
        "afraid",
        "done",
        "asking",
        "wondering"
    };

    /// The label or learned name for this speaker in this session.
    public static string DisplayName(string sessionId, string speakerId)
    {
        var roster = Rosters.GetOrAdd(sessionId, _ => new Dictionary<string, Entry>());
        lock (roster)
        {
            if (roster.TryGetValue(speakerId, out var existing)) return existing.Name ?? existing.Label;

            var label = $"Soldier {roster.Count + 1}";
            roster[speakerId] = new Entry(label, null);
            return label;
        }
    }

    /// Spot an introduction in what the player said. Returns the display the speaker had
    /// BEFORE this call plus the learned name, or null when nothing was learned. Only the
    /// word they gave is kept, first-letter capitalised — "I'm beswick" lands as "Beswick",
    /// and a callsign never reaches the transcript at all.
    public static (string OldDisplay, string NewName)? LearnName(string sessionId, string speakerId, string text)
    {
        var match = Introduction.Match(text ?? string.Empty);
        if (!match.Success) return null;

        var name = match.Groups[1].Value;
        if (NotNames.Contains(name)) return null;

        name = char.ToUpperInvariant(name[0]) + name[1..];
        var roster = Rosters.GetOrAdd(sessionId, _ => new Dictionary<string, Entry>());
        lock (roster)
        {
            var old = roster.TryGetValue(speakerId, out var existing) ? existing.Name ?? existing.Label : speakerId;
            if (old == name) return null; // already known under this name

            roster[speakerId] = new Entry(existing?.Label ?? $"Soldier {roster.Count + 1}", name);
            return (old, name);
        }
    }

    public static void Reset(string sessionId) => Rosters.TryRemove(sessionId, out _);
}
