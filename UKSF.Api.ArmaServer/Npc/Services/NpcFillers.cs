using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// The latency-mask filler set, as a voice-level asset.
///
/// Fillers are rendered once per voice by the mood worker and stored on disk next to the
/// voice, because the engine that voices them well is the heavy one: the clone engine
/// renders a bare interjection as near-silence (measured rms 7 against 2700 for speech),
/// while the emotion engine voices it properly. That engine must not be spun up at
/// mission time while a game holds the GPU, so nothing here may be rendered at
/// registration — registration only pushes what already exists on disk.
///
/// Every entry is a single interjection spelled the way it is said. Compound noises and
/// letter elongation come out of the engine as phonetic mush.
public static class NpcFillers
{
    public const string EmoText = "hesitant, thoughtful, considering";
    public const double EmoAlpha = 0.7;

    /// s-class for a short wait, l-class for a long one; see Clark &amp; Fox Tree 2002.
    public static readonly (string Id, string Text)[] Table =
    [
        ("s0", "Hm."), ("s1", "Uh..."), ("s2", "Mm."), ("s3", "Ah."), ("s4", "Oh."), ("s5", "Mhm."), ("s6", "Huh."), ("s7", "Uh huh."),
        ("l0", "Umm..."), ("l1", "Hmm..."), ("l2", "Uhhh..."), ("l3", "Hmh..."), ("l4", "Ummm...")
    ];

    /// The words in the clip, as a readable file slug: "Uh huh." -> "Uh huh", "Umm..." -> "Umm".
    public static string Slug(string text)
    {
        var chars = text.Where(c => char.IsLetter(c) || c == ' ').ToArray();
        return new string(chars).Trim();
    }

    public static string RelativePath(string voiceId, string fillerId)
    {
        var text = Table.First(t => t.Id == fillerId).Text;
        return NpcVoiceStore.FillerPath(voiceId, Slug(text));
    }
}
