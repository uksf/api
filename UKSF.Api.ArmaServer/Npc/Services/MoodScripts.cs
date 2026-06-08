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
    public const double EmoAlpha = 0.8;

    public record Entry(string EmoText, string Script);

    // emoText = IndexTTS-2 natural-language emotion description; Script = the ~8s line it reads
    // in the cloned timbre to become a {base}_{mood} reference clip.
    public static readonly IReadOnlyDictionary<string, Entry> Table = new Dictionary<string, Entry>
    {
        ["angry"] =
            new(
                "furious, harsh and contemptuous",
                "I have had enough of your excuses. Every single time, you let us down. Do not test my patience again, soldier."
            ),
        ["afraid"] = new(
            "panicked, fearful and trembling",
            "They're flanking us — fall back, fall back now! I can't hold this position, there's too many of them. Somebody get on the radio!"
        ),
        ["sad"] = new(
            "grief-stricken, sorrowful and weary",
            "I do not know if any of us make it through this. So many gone already. I just wanted to see home one more time."
        ),
        ["happy"] = new(
            "joyful, excited and elated",
            "We did it. We actually did it! After everything we have been through, we are finally going home. I can hardly believe it!"
        )
    };

    public static readonly IReadOnlyList<string> Generated = Table.Keys.ToList();
    public static readonly IReadOnlyList<string> All = new[] { Neutral }.Concat(Generated).ToList();

    public static bool IsValid(string mood) => All.Contains(mood);
}
