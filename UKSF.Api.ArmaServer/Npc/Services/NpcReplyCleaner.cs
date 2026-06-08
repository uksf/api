using System.Text.RegularExpressions;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// <summary>
/// Defensive cleanup of dynamic NPC replies before they reach TTS/history.
/// Qwen2.5-3B leaks a leading "You said:" transcript framing ~35% of the time
/// (catalogue-lock bench, npc_quality_results.json); TTS-unsafe characters would
/// be read aloud literally. ExtractMood pulls the leading [mood:x] tag the model is
/// asked to emit — it MUST run before Clean (which strips brackets).
/// </summary>
public static partial class NpcReplyCleaner
{
    public static string Clean(string raw)
    {
        var text = raw.Trim();
        text = YouSaidPrefix().Replace(text, "");
        text = TtsUnsafe().Replace(text, "");
        return text.Trim();
    }

    /// <summary>Returns (mood, remainingText). Unknown/absent tag → ("neutral", original text).</summary>
    public static (string Mood, string Text) ExtractMood(string raw)
    {
        var match = MoodTag().Match(raw);
        if (!match.Success)
        {
            return (MoodScripts.Neutral, raw.Trim());
        }

        var mood = match.Groups[1].Value.ToLowerInvariant();
        var rest = raw[match.Length..].Trim();
        return MoodScripts.IsValid(mood) ? (mood, rest) : (MoodScripts.Neutral, rest);
    }

    [GeneratedRegex(@"^\s*you said:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex YouSaidPrefix();

    [GeneratedRegex("[*\\[\\]()\"]")]
    private static partial Regex TtsUnsafe();

    [GeneratedRegex(@"^\s*\[mood:\s*([a-zA-Z]+)\s*\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex MoodTag();
}
