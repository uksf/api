using System.Text.RegularExpressions;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordTextService
{
    string FromMarkdown(string markdown);
    string ToQuote(string text);
}

public class DiscordTextService : IDiscordTextService
{
    private static readonly Dictionary<string, string> Replacements = new()
    {
        { "(#### )(.*)\n", "**$2**\n" },
        { "<br>", "\n" },
        { "\nSR3", "SR3" },
        { @"(\[.*?\])\((.*?)\)", "$1(<$2>)" },
    };

    public string FromMarkdown(string markdown)
    {
        var text = Replacements.Aggregate(markdown, (text, replacement) => Regex.Replace(text, replacement.Key, replacement.Value));

        return text;
    }

    public string ToQuote(string text)
    {
        return text.Insert(0, "> ").Replace("\n", "\n> ");
    }
}
