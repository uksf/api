using System;
using System.Text;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class NpcTextSanitiser
{
    private const int MaxLength = 500;

    private static readonly string[] JunkTokens =
    [
        "[BLANK_AUDIO]",
        "[BLANK AUDIO]",
        "[BLANK]",
        "[SILENCE]",
        "[MUSIC]",
        "(BLANK)"
    ];

    public static string Sanitise(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var collapsed = CollapseWhitespace(input);
        if (collapsed.Length == 0) return string.Empty;

        foreach (var token in JunkTokens)
        {
            collapsed = ReplaceInsensitive(collapsed, token, " ");
        }

        collapsed = CollapseWhitespace(collapsed);
        if (collapsed.Length == 0 || IsJunkOnly(collapsed)) return string.Empty;
        return collapsed.Length > MaxLength ? collapsed[..MaxLength] : collapsed;
    }

    private static bool IsJunkOnly(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) folded.Append(char.ToLowerInvariant(c));
        }

        return folded.ToString() is "blankaudio" or "blank" or "silence" or "music";
    }

    private static string CollapseWhitespace(string input)
    {
        var builder = new StringBuilder(input.Length);
        var lastWasSpace = false;
        foreach (var c in input)
        {
            var isSpace = char.IsControl(c) || char.IsWhiteSpace(c);
            if (isSpace)
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                builder.Append(c);
                lastWasSpace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static string ReplaceInsensitive(string haystack, string needle, string replacement)
    {
        var current = haystack;
        while (true)
        {
            var index = current.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return current;
            current = current.Remove(index, needle.Length).Insert(index, replacement);
        }
    }
}
