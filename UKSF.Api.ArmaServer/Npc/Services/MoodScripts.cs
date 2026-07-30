using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// <summary>
/// Single source of truth for NPC moods. neutral is the base voice itself (never generated);
/// the four emotional moods are produced by the auto-gen pipeline and selectable at runtime.
/// </summary>
public static class MoodScripts
{
    public const string Neutral = "neutral";

    /// A mood reference is the emotion engine's rendering of the base voice, and the runtime
    /// engine then clones from it, so the two engines' differences compound. Strong emotion
    /// transfer drags the timbre with it and the NPC audibly changes person when their mood
    /// turns. Held low enough that the mood reads while the character stays the same man.
    public const double EmoAlpha = 0.55;

    public record Entry(string EmoText, string Script);

    /// <summary>
    /// Every mood reads the SAME neutral script. The clip is a timbre reference for the runtime
    /// clone engine, which carries no emotion of its own — so the emotion must live in the
    /// delivery, and only in the delivery. Mood-specific wording made the reference clips sound
    /// like four different scenes and muddied the ear-check: a listener could not tell whether
    /// the anger was in the voice or in the words. Neutral wording isolates it.
    ///
    /// Content is deliberately plain and situation-free. Every NPC voice is male — Arma has no
    /// female character models — so nothing here may imply a speaker's gender.
    /// </summary>
    public const string Script = "Check the map and mark the crossing before you set off. " +
                                 "The road bends north past the treeline, then follows the river for about a mile. " +
                                 "Keep the radio on and tell me what you find when you get there.";

    // emoText = IndexTTS-2 natural-language emotion description, the only thing that varies.
    //
    // neutral is generated like any other mood. The uploaded sample is only ever a seed now:
    // using it directly for neutral meant neutral came from one engine and every mood from
    // another, and the NPC did not merely change timbre between them, he changed person.
    // Every mood a player hears is the same engine's rendering of the same seed.
    public static readonly IReadOnlyDictionary<string, Entry> Table = new Dictionary<string, Entry>
    {
        [Neutral] = new("calm, even and matter-of-fact", Script),
        ["angry"] = new("furious, harsh and contemptuous", Script),
        ["afraid"] = new("panicked, fearful and trembling", Script),
        ["sad"] = new("grief-stricken, sorrowful and weary", Script),
        ["happy"] = new("joyful, excited and elated", Script)
    };

    public static readonly IReadOnlyList<string> Generated = Table.Keys.ToList();
    public static readonly IReadOnlyList<string> All = Generated;

    public static bool IsValid(string mood) => All.Contains(mood);
}
