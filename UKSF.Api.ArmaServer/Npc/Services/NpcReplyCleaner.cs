using System.Text.RegularExpressions;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// <summary>
/// Defensive cleanup of dynamic NPC replies before they reach TTS/history.
/// Qwen2.5-3B leaks a leading "You said:" transcript framing ~35% of the time
/// (catalogue-lock bench, npc_quality_results.json); TTS-unsafe characters would
/// be read aloud literally.
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

    [GeneratedRegex(@"^\s*you said:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex YouSaidPrefix();

    [GeneratedRegex("[*\\[\\]()\"]")]
    private static partial Regex TtsUnsafe();
}
